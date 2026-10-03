using System.Net;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Pdf.IO;

namespace MedFlow.Api.Tests;

public class PrescriptionPdfTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public PrescriptionPdfTests(TestApiFactory f) => _f = f;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<(AuthResult doctor, int pid)> SetupAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var doctor = await _f.RegisterDoctorAsync($"doc-{tag}@x.com");
        return (doctor, await _f.CreatePatientAsync(doctor.Token, $"pat-{tag}@x.com"));
    }

    private Task<int> SeedRxAsync(string doctorUserId, int pid, string drug = "Amoxicillin",
        string? instructions = "Take with food", PrescriptionStatus status = PrescriptionStatus.Active, int expiresInDays = 30) =>
        _f.WithDbAsync(async db =>
        {
            var rx = new Prescription
            {
                PatientId = pid, DoctorId = doctorUserId, DrugName = drug, Dosage = "500 mg", Frequency = "Twice daily",
                Instructions = instructions, IssuedDate = Today, ExpiryDate = Today.AddDays(expiresInDays),
                RefillsRemaining = 2, Status = status
            };
            db.Prescriptions.Add(rx);
            await db.SaveChangesAsync();
            return rx.Id;
        });

    private Task<List<AuditEvent>> ViewsAsync(int pid, int rxId) =>
        _f.WithDbAsync(db => db.AuditEvents
            .Where(e => e.PatientId == pid && e.ItemKind == AuditItemKind.Prescription && e.ItemId == rxId && e.Action == AuditAction.View)
            .ToListAsync());

    private static int PageCount(byte[] pdf)
    {
        using var ms = new MemoryStream(pdf);
        return PdfReader.Open(ms, PdfDocumentOpenMode.Import).PageCount;
    }

    private static PrescriptionDocumentData Data(string drug = "Amoxicillin", string? instructions = "Take with food",
        PrescriptionStatus status = PrescriptionStatus.Active, int expiresInDays = 30, string patient = "Pat Ient",
        string? license = "LIC-123", string? address = "1 Main St, Springfield IL 62701") =>
        new(7, 3, "Dr. Doc Tor", "Cardiology", license, "555-0100", patient, new DateOnly(1990, 1, 2), "555-0199", address,
            drug, "500 mg", "Twice daily", instructions, Today, Today.AddDays(expiresInDays), 2, status);

    // ── Endpoint: success, headers, audit ─────────────────────────────────────

    [Fact]
    public async Task Owner_gets_pdf_with_no_store_and_one_audit_view()
    {
        var (d, pid) = await SetupAsync();
        var id = await SeedRxAsync(d.UserId, pid);

        var res = await _f.ClientFor(d.Token).GetAsync($"/api/prescriptions/{id}/pdf");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/pdf", res.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-store", res.Headers.CacheControl!.ToString());
        Assert.Equal($"prescription-{id}.pdf", res.Content.Headers.ContentDisposition!.FileName);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.Equal(1, PageCount(bytes));

        var view = Assert.Single(await ViewsAsync(pid, id));
        Assert.Equal(d.UserId, view.ActorUserId);
        Assert.Equal("Doctor", view.ActorRole);

        await _f.ClientFor(d.Token).GetAsync($"/api/prescriptions/{id}/pdf");
        Assert.Equal(2, (await ViewsAsync(pid, id)).Count); // every print is logged
    }

    // ── Access control and isolation ──────────────────────────────────────────

    [Fact]
    public async Task Other_doctor_gets_404_identical_to_missing_and_nothing_is_audited()
    {
        var (owner, pid) = await SetupAsync();
        var id = await SeedRxAsync(owner.UserId, pid);
        var other = await _f.RegisterDoctorAsync($"doc-{Guid.NewGuid():N}@x.com");
        var c = _f.ClientFor(other.Token);

        var foreign = await c.GetAsync($"/api/prescriptions/{id}/pdf");
        var missing = await c.GetAsync("/api/prescriptions/99999999/pdf");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        static string NoTrace(string b) => System.Text.RegularExpressions.Regex.Replace(b, "\"traceId\":\"[^\"]*\"", "");
        Assert.Equal(NoTrace(await missing.Content.ReadAsStringAsync()), NoTrace(await foreign.Content.ReadAsStringAsync()));
        Assert.Equal(missing.Content.Headers.ContentType?.MediaType, foreign.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await ViewsAsync(pid, id));
    }

    [Fact]
    public async Task Doctor_cannot_print_own_prescription_written_for_another_doctors_patient()
    {
        var (owner, pid) = await SetupAsync();
        var other = await _f.RegisterDoctorAsync($"doc-{Guid.NewGuid():N}@x.com");
        var id = await SeedRxAsync(other.UserId, pid); // prescriber is "other", patient belongs to "owner"

        Assert.Equal(HttpStatusCode.NotFound, (await _f.ClientFor(other.Token).GetAsync($"/api/prescriptions/{id}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _f.ClientFor(owner.Token).GetAsync($"/api/prescriptions/{id}/pdf")).StatusCode);
        Assert.Empty(await ViewsAsync(pid, id));
    }

    [Fact]
    public async Task Patient_token_is_rejected_and_anonymous_is_unauthorised()
    {
        var (d, pid) = await SetupAsync();
        var id = await SeedRxAsync(d.UserId, pid);
        var email = $"portal-{Guid.NewGuid():N}@x.com";
        var pid2 = await _f.CreatePatientAsync(d.Token, email);
        var patient = await _f.OnboardPatientAsync(d.Token, pid2, email);

        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(patient.Token).GetAsync($"/api/prescriptions/{id}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync($"/api/prescriptions/{id}/pdf")).StatusCode);
        Assert.Empty(await ViewsAsync(pid, id));
    }

    // ── Document content (via the renderer) ───────────────────────────────────

    private static List<string> Text(PrescriptionDocumentData data, out byte[] pdf)
    {
        var sink = new List<string>();
        pdf = new PrescriptionPdfRenderer().Render(data, sink);
        return sink;
    }

    [Fact]
    public void Document_has_doctor_patient_medication_and_blank_signature_block()
    {
        var text = Text(Data(), out var pdf);
        var all = string.Join("\n", text);
        foreach (var expected in new[] { "Dr. Doc Tor", "Cardiology", "LIC-123", "555-0100", "Pat Ient", "Jan 2, 1990",
                     "555-0199", "1 Main St,", "Springfield", "Amoxicillin", "500 mg", "Twice daily", "Take with food",
                     "Prescriber signature", "Date" })
            Assert.Contains(expected, all);
        Assert.DoesNotContain("NOT VALID", all);
        Assert.DoesNotContain("electronically signed", all, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, PageCount(pdf));
    }

    [Fact]
    public void Missing_optional_fields_are_omitted_without_error()
    {
        var all = string.Join("\n", Text(Data(instructions: null, license: null, address: null), out var pdf));
        Assert.DoesNotContain("Licence No.", all);
        Assert.DoesNotContain("Address:", all);
        Assert.DoesNotContain("Instructions:", all);
        Assert.Equal(1, PageCount(pdf));
    }

    [Theory]
    [InlineData(PrescriptionStatus.Cancelled, 30, "NOT VALID - CANCELLED")]
    [InlineData(PrescriptionStatus.Expired, -3, "NOT VALID - EXPIRED")]
    [InlineData(PrescriptionStatus.Active, -3, "NOT VALID - EXPIRED")]
    public void Non_valid_prescriptions_carry_a_banner(PrescriptionStatus status, int days, string banner)
    {
        Assert.Contains(banner, Text(Data(status: status, expiresInDays: days), out _));
    }

    [Fact]
    public void Expiring_soon_prescription_is_still_valid()
    {
        Assert.DoesNotContain(Text(Data(status: PrescriptionStatus.ExpiringSoon, expiresInDays: 5), out _), t => t.StartsWith("NOT VALID"));
    }

    [Fact]
    public void Very_long_values_stay_on_one_page()
    {
        var longText = string.Join(" ", Enumerable.Repeat("Take one tablet by mouth after meals and rest.", 300));
        var text = Text(Data(drug: new string('X', 400), instructions: longText, patient: new string('P', 300)), out var pdf);
        Assert.Equal(1, PageCount(pdf));
        Assert.Contains("Prescriber signature", text);
    }

    [Fact]
    public void Accented_spanish_text_renders()
    {
        var text = Text(Data(patient: "José Peña Núñez", instructions: "Tomar después de comer, ¿sí?"), out var pdf);
        Assert.Contains("José Peña Núñez", text);
        Assert.True(pdf.Length > 1000);
    }

    [Fact]
    public async Task Document_never_contains_email_insurance_allergies_or_notes()
    {
        var (d, pid) = await SetupAsync();
        await _f.WithDbAsync(async db =>
        {
            var p = await db.Patients.FirstAsync(x => x.Id == pid);
            p.Email = "SECRET-EMAIL@x.com"; p.InsuranceProvider = "SECRET-INS"; p.Allergies = "SECRET-ALLERGY"; p.Notes = "SECRET-NOTES";
            await db.SaveChangesAsync();
            return 0;
        });
        var id = await SeedRxAsync(d.UserId, pid);
        var data = await _f.WithDbAsync(async db =>
        {
            var repo = new MedFlow.Infrastructure.Repositories.PrescriptionRepository(db);
            return await repo.GetDocumentDataAsync(id, d.UserId);
        });
        Assert.NotNull(data);
        var all = string.Join("\n", Text(data!, out _));
        Assert.DoesNotContain("SECRET", all);
    }
}
