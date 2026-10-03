using MedFlow.Core.Entities;
using MedFlow.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace MedFlow.Api.Tests;

/// <summary>
/// The migration SQL cannot run on the in-memory provider used by these tests, so these guards check its structure:
/// every clinic-scoped table is backfilled, in the right order, nothing is dropped by accident and Down is safe.
/// (Running the SQL against a real SQL Server database is a manual step listed in the spec quickstart.)
/// </summary>
public class ClinicBackfillTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public ClinicBackfillTests(TestApiFactory f) => _f = f;

    private static readonly AddClinicsAndRoles Migration = new();

    private static IReadOnlyList<MigrationOperation> Up => Migration.UpOperations;
    private static IReadOnlyList<MigrationOperation> Down => Migration.DownOperations;

    private static List<string> Sql(IEnumerable<MigrationOperation> ops) =>
        ops.OfType<SqlOperation>().Select(o => o.Sql).ToList();

    // Table names come from the SQL Server model (no connection is opened), not from the in-memory test provider
    private static Task<HashSet<string>> ScopedTablesAsync()
    {
        using var db = new MedFlow.Infrastructure.Data.AppDbContext(
            new DbContextOptionsBuilder<MedFlow.Infrastructure.Data.AppDbContext>().UseSqlServer("Server=unused;Database=unused").Options);
        var model = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>(db).Model;
        return Task.FromResult(model.GetEntityTypes().Where(t => typeof(IClinicScoped).IsAssignableFrom(t.ClrType))
            .Select(t => t.GetTableName()!).ToHashSet());
    }

    [Fact]
    public async Task Migration_backfills_exactly_the_clinic_scoped_tables()
    {
        var scoped = await ScopedTablesAsync();
        var expected = new HashSet<string>(AddClinicsAndRoles.PatientOwnedTables) { "Patients" };
        Assert.True(expected.SetEquals(scoped), $"model: {string.Join(",", scoped.OrderBy(x => x))}");

        var statements = Sql(Up);
        Assert.Contains(statements, s => s.Contains("UPDATE p SET p.[ClinicId] = c.[Id] FROM [Patients] p"));
        foreach (var table in AddClinicsAndRoles.PatientOwnedTables)
            Assert.Contains(statements, s => s.Contains($"UPDATE t SET t.[ClinicId] = p.[ClinicId] FROM [{table}] t"));

        // rows whose patient gave no clinic fall back to their doctor (every doctor-keyed table; vitals have no doctor column)
        var fallback = new HashSet<string>(AddClinicsAndRoles.DoctorKeyedTables);
        Assert.True(fallback.SetEquals(expected.Except(new[] { "Patients", "VitalSigns" })));
        foreach (var table in fallback)
            Assert.Contains(statements, s => s.Contains($"FROM [{table}] t JOIN [Clinics] c ON c.[LegacyDoctorUserId] = t.[DoctorId] WHERE t.[ClinicId] = 0"));
    }

    [Fact]
    public async Task Every_clinic_column_is_added_not_null_with_default_zero_once()
    {
        var scoped = await ScopedTablesAsync();
        var adds = Up.OfType<AddColumnOperation>().Where(o => o.Name == "ClinicId").ToList();
        Assert.Equal(scoped.Count, adds.Count);
        Assert.True(scoped.SetEquals(adds.Select(a => a.Table)));
        Assert.All(adds, a =>
        {
            Assert.False(a.IsNullable);
            Assert.Equal(0, a.DefaultValue);
            Assert.Equal(typeof(int), a.ClrType);
        });
    }

    [Fact]
    public void One_clinic_and_an_owner_membership_are_created_per_doctor()
    {
        var statements = Sql(Up).Select(s => s.Replace("''", "'")).ToList();
        var clinics = Assert.Single(statements, s => s.Contains("INSERT INTO [Clinics]"));
        Assert.Contains("FROM [Doctors] d", clinics);
        Assert.Contains("d.[UserId]", clinics);
        Assert.DoesNotContain("WHERE", clinics); // every doctor, including soft-deleted ones
        var members = Assert.Single(statements, s => s.Contains("INSERT INTO [ClinicMembers]"));
        Assert.Contains("N'Owner'", members);
        Assert.Contains(", 1, SYSUTCDATETIME()", members); // IsActive
        Assert.Contains("FROM [Clinics] c", members);
    }

    [Fact]
    public async Task Backfill_runs_after_the_columns_and_before_indexes_and_foreign_keys()
    {
        var ops = Up.ToList();
        int Index(Func<MigrationOperation, bool> match) => ops.FindIndex(o => match(o));
        int Last(Func<MigrationOperation, bool> match) => ops.FindLastIndex(o => match(o));

        var lastAddColumn = Last(o => o is AddColumnOperation { Name: "ClinicId" });
        var legacyAdd = Index(o => o is AddColumnOperation { Name: "LegacyDoctorUserId" });
        var firstBackfill = Index(o => o is SqlOperation s && s.Sql.Contains("INSERT INTO [Clinics]"));
        var lastBackfill = Last(o => o is SqlOperation s && s.Sql.Contains("[ClinicId]") && !s.Sql.Contains("THROW"));
        var guard = Index(o => o is SqlOperation s && s.Sql.Contains("THROW"));
        var legacyDrop = Index(o => o is DropColumnOperation { Name: "LegacyDoctorUserId" });
        var firstIndex = Index(o => o is CreateIndexOperation { Name: "IX_Patients_ClinicId" });
        var firstFk = Index(o => o is AddForeignKeyOperation { PrincipalTable: "Clinics" } and not { Table: "ClinicMembers" or "StaffInvitations" });

        Assert.True(lastAddColumn >= 0 && legacyAdd >= 0 && firstBackfill >= 0 && guard >= 0 && legacyDrop >= 0 && firstIndex >= 0 && firstFk >= 0);
        Assert.True(Index(o => o is CreateTableOperation { Name: "Clinics" }) < legacyAdd);
        Assert.True(legacyAdd < firstBackfill, "temporary mapping column exists before it is used");
        Assert.True(lastAddColumn < firstBackfill, "ClinicId columns exist before they are updated");
        Assert.True(lastBackfill < guard, "the guard checks the finished backfill");
        Assert.True(guard < legacyDrop);
        Assert.True(legacyDrop < firstIndex && legacyDrop < firstFk, "indexes and FKs are created only after the backfill");

        // the guard covers every scoped table, so no record can be left with ClinicId = 0
        var guardSql = Sql(Up).Single(s => s.Contains("THROW"));
        foreach (var table in await ScopedTablesAsync())
            Assert.Contains($"[{table}] WHERE [ClinicId] = 0", guardSql);
    }

    [Fact]
    public void Up_destroys_nothing_beyond_the_notes_doctor_foreign_key()
    {
        Assert.Empty(Up.OfType<DropTableOperation>());
        Assert.Empty(Up.OfType<RenameColumnOperation>());
        Assert.Empty(Up.OfType<RenameTableOperation>());
        Assert.Empty(Up.OfType<AlterColumnOperation>());
        Assert.Empty(Up.OfType<DeleteDataOperation>());
        Assert.Equal(new[] { "LegacyDoctorUserId" }, Up.OfType<DropColumnOperation>().Select(o => o.Name));
        var fk = Assert.Single(Up.OfType<DropForeignKeyOperation>());
        Assert.Equal(("MedicalNotes", "FK_MedicalNotes_Doctors_DoctorId"), (fk.Table, fk.Name));
        Assert.Equal(new[] { "IX_MedicalNotes_DoctorId" }, Up.OfType<DropIndexOperation>().Select(o => o.Name));
        Assert.All(Sql(Up), s =>
        {
            Assert.DoesNotContain("DELETE ", s, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TRUNCATE", s, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DROP ", s, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Down_only_removes_what_up_added_and_restores_the_notes_foreign_key_safely()
    {
        var scoped = await ScopedTablesAsync();
        Assert.True(new HashSet<string> { "Clinics", "ClinicMembers", "StaffInvitations" }.SetEquals(Down.OfType<DropTableOperation>().Select(o => o.Name)));
        var dropped = Down.OfType<DropColumnOperation>().ToList();
        Assert.All(dropped, d => Assert.Equal("ClinicId", d.Name));
        Assert.True(scoped.SetEquals(dropped.Select(d => d.Table)));
        Assert.Empty(Down.OfType<AlterColumnOperation>());
        Assert.Empty(Down.OfType<DeleteDataOperation>());

        var ops = Down.ToList();
        var reassign = ops.FindIndex(o => o is SqlOperation s && s.Sql.StartsWith("UPDATE n SET n.[DoctorId]") && s.Sql.Contains("NOT EXISTS (SELECT 1 FROM [Doctors] d WHERE d.[UserId] = n.[DoctorId])"));
        var restore = ops.FindIndex(o => o is AddForeignKeyOperation { Name: "FK_MedicalNotes_Doctors_DoctorId" });
        Assert.True(reassign >= 0 && restore >= 0 && reassign < restore, "non-doctor note authors are reassigned before the FK is restored");
        Assert.Contains(ops, o => o is CreateIndexOperation { Name: "IX_MedicalNotes_DoctorId" });
    }
}
