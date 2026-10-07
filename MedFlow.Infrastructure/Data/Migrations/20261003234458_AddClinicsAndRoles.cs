using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicsAndRoles : Migration
    {
        /// <summary>Tables whose rows have a PatientId; their clinic is the patient's clinic.</summary>
        public static readonly string[] PatientOwnedTables =
        {
            "Appointments", "Prescriptions", "Invoices", "VitalSigns", "MedicalNotes", "PatientAttachments",
            "PatientAllergies", "PatientProblems", "PatientMedications", "LabOrders", "IntakeSubmissions",
            "WaitlistEntries", "AuditEvents"
        };

        /// <summary>Fallback for rows whose patient could not provide a clinic (VitalSigns has no doctor column).</summary>
        public static readonly string[] DoctorKeyedTables =
        {
            "Appointments", "Prescriptions", "Invoices", "MedicalNotes", "PatientAttachments",
            "PatientAllergies", "PatientProblems", "PatientMedications", "LabOrders", "IntakeSubmissions",
            "WaitlistEntries", "AuditEvents"
        };

        private static void Exec(Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder migrationBuilder, string sql) =>
            migrationBuilder.Sql("EXEC(N'" + sql.Replace("'", "''") + "')");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MedicalNotes_Doctors_DoctorId",
                table: "MedicalNotes");

            migrationBuilder.DropIndex(
                name: "IX_MedicalNotes_DoctorId",
                table: "MedicalNotes");

            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "WaitlistEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "VitalSigns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "Prescriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "Patients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "PatientProblems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "PatientMedications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "PatientAttachments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "PatientAllergies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "MedicalNotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "LabOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "IntakeSubmissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "AuditEvents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "Appointments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Clinics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Stamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clinics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClinicMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicMembers_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClinicId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvitedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffInvitations_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ── Backfill ──────────────────────────────────────────────────────────────────
            // Every existing doctor gets a clinic of their own with an active Owner membership, and every record takes the
            // clinic of its patient (falling back to its doctor). Nothing is deleted or rewritten besides the new ClinicId.
            // A temporary Clinics.LegacyDoctorUserId maps doctors to their clinic and is dropped again below.
            migrationBuilder.AddColumn<string>(
                name: "LegacyDoctorUserId",
                table: "Clinics",
                type: "nvarchar(450)",
                nullable: true);

            // Raw SQL runs through EXEC so the script also compiles inside idempotent migration scripts
            Exec(migrationBuilder,
                "INSERT INTO [Clinics] ([Name], [Stamp], [CreatedAt], [UpdatedAt], [IsDeleted], [LegacyDoctorUserId]) " +
                "SELECT LEFT(N'Clinic of ' + LTRIM(RTRIM(d.[FirstName] + N' ' + d.[LastName])), 200), NEWID(), SYSUTCDATETIME(), SYSUTCDATETIME(), 0, d.[UserId] " +
                "FROM [Doctors] d");
            Exec(migrationBuilder,
                "INSERT INTO [ClinicMembers] ([ClinicId], [UserId], [Role], [IsActive], [CreatedAt], [UpdatedAt], [IsDeleted]) " +
                "SELECT c.[Id], c.[LegacyDoctorUserId], N'Owner', 1, SYSUTCDATETIME(), SYSUTCDATETIME(), 0 " +
                "FROM [Clinics] c WHERE c.[LegacyDoctorUserId] IS NOT NULL");
            Exec(migrationBuilder,
                "UPDATE p SET p.[ClinicId] = c.[Id] FROM [Patients] p " +
                "JOIN [Clinics] c ON c.[LegacyDoctorUserId] = p.[DoctorId]");
            foreach (var table in PatientOwnedTables)
                Exec(migrationBuilder,
                    $"UPDATE t SET t.[ClinicId] = p.[ClinicId] FROM [{table}] t " +
                    "JOIN [Patients] p ON p.[Id] = t.[PatientId] WHERE p.[ClinicId] <> 0");
            foreach (var table in DoctorKeyedTables)
                Exec(migrationBuilder,
                    $"UPDATE t SET t.[ClinicId] = c.[Id] FROM [{table}] t " +
                    "JOIN [Clinics] c ON c.[LegacyDoctorUserId] = t.[DoctorId] WHERE t.[ClinicId] = 0");

            // Refuse to finish (and roll the migration back) if any record would be left without a clinic
            var unscoped = string.Join(" OR ", new[] { "Patients" }.Concat(PatientOwnedTables)
                .Select(t => $"EXISTS (SELECT 1 FROM [{t}] WHERE [ClinicId] = 0)"));
            Exec(migrationBuilder,
                $"IF {unscoped} THROW 50000, 'AddClinicsAndRoles: some records could not be assigned to a clinic.', 1");

            migrationBuilder.DropColumn(
                name: "LegacyDoctorUserId",
                table: "Clinics");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_ClinicId",
                table: "WaitlistEntries",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_VitalSigns_ClinicId",
                table: "VitalSigns",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_ClinicId",
                table: "Prescriptions",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_ClinicId",
                table: "Patients",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientProblems_ClinicId",
                table: "PatientProblems",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedications_ClinicId",
                table: "PatientMedications",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAttachments_ClinicId",
                table: "PatientAttachments",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAllergies_ClinicId",
                table: "PatientAllergies",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalNotes_ClinicId",
                table: "MedicalNotes",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_ClinicId",
                table: "LabOrders",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClinicId",
                table: "Invoices",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSubmissions_ClinicId",
                table: "IntakeSubmissions",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ClinicId",
                table: "AuditEvents",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ClinicId",
                table: "Appointments",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicMembers_ClinicId_Role_IsActive",
                table: "ClinicMembers",
                columns: new[] { "ClinicId", "Role", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicMembers_UserId",
                table: "ClinicMembers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvitations_ClinicId_Email",
                table: "StaffInvitations",
                columns: new[] { "ClinicId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvitations_TokenHash",
                table: "StaffInvitations",
                column: "TokenHash");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Clinics_ClinicId",
                table: "Appointments",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditEvents_Clinics_ClinicId",
                table: "AuditEvents",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeSubmissions_Clinics_ClinicId",
                table: "IntakeSubmissions",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Clinics_ClinicId",
                table: "Invoices",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LabOrders_Clinics_ClinicId",
                table: "LabOrders",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalNotes_Clinics_ClinicId",
                table: "MedicalNotes",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientAllergies_Clinics_ClinicId",
                table: "PatientAllergies",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientAttachments_Clinics_ClinicId",
                table: "PatientAttachments",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientMedications_Clinics_ClinicId",
                table: "PatientMedications",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientProblems_Clinics_ClinicId",
                table: "PatientProblems",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_Clinics_ClinicId",
                table: "Patients",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_Clinics_ClinicId",
                table: "Prescriptions",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VitalSigns_Clinics_ClinicId",
                table: "VitalSigns",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WaitlistEntries_Clinics_ClinicId",
                table: "WaitlistEntries",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Clinics_ClinicId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditEvents_Clinics_ClinicId",
                table: "AuditEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_IntakeSubmissions_Clinics_ClinicId",
                table: "IntakeSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Clinics_ClinicId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_LabOrders_Clinics_ClinicId",
                table: "LabOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_MedicalNotes_Clinics_ClinicId",
                table: "MedicalNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientAllergies_Clinics_ClinicId",
                table: "PatientAllergies");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientAttachments_Clinics_ClinicId",
                table: "PatientAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientMedications_Clinics_ClinicId",
                table: "PatientMedications");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientProblems_Clinics_ClinicId",
                table: "PatientProblems");

            migrationBuilder.DropForeignKey(
                name: "FK_Patients_Clinics_ClinicId",
                table: "Patients");

            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_Clinics_ClinicId",
                table: "Prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_VitalSigns_Clinics_ClinicId",
                table: "VitalSigns");

            migrationBuilder.DropForeignKey(
                name: "FK_WaitlistEntries_Clinics_ClinicId",
                table: "WaitlistEntries");

            migrationBuilder.DropTable(
                name: "ClinicMembers");

            migrationBuilder.DropTable(
                name: "StaffInvitations");

            migrationBuilder.DropTable(
                name: "Clinics");

            migrationBuilder.DropIndex(
                name: "IX_WaitlistEntries_ClinicId",
                table: "WaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_VitalSigns_ClinicId",
                table: "VitalSigns");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_ClinicId",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_Patients_ClinicId",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_PatientProblems_ClinicId",
                table: "PatientProblems");

            migrationBuilder.DropIndex(
                name: "IX_PatientMedications_ClinicId",
                table: "PatientMedications");

            migrationBuilder.DropIndex(
                name: "IX_PatientAttachments_ClinicId",
                table: "PatientAttachments");

            migrationBuilder.DropIndex(
                name: "IX_PatientAllergies_ClinicId",
                table: "PatientAllergies");

            migrationBuilder.DropIndex(
                name: "IX_MedicalNotes_ClinicId",
                table: "MedicalNotes");

            migrationBuilder.DropIndex(
                name: "IX_LabOrders_ClinicId",
                table: "LabOrders");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ClinicId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_IntakeSubmissions_ClinicId",
                table: "IntakeSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_ClinicId",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_ClinicId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "WaitlistEntries");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "VitalSigns");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "PatientProblems");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "PatientMedications");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "PatientAttachments");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "PatientAllergies");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "MedicalNotes");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "IntakeSubmissions");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "Appointments");

            // Notes written by non-doctor staff (nurses) have no Doctors row: attribute them to the patient's treating doctor
            // so the original foreign key can be restored. This is the only authorship information lost on rollback.
            migrationBuilder.Sql(
                "UPDATE n SET n.[DoctorId] = p.[DoctorId] FROM [MedicalNotes] n " +
                "JOIN [Patients] p ON p.[Id] = n.[PatientId] " +
                "WHERE NOT EXISTS (SELECT 1 FROM [Doctors] d WHERE d.[UserId] = n.[DoctorId])");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalNotes_DoctorId",
                table: "MedicalNotes",
                column: "DoctorId");

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalNotes_Doctors_DoctorId",
                table: "MedicalNotes",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
