using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedFlow.Api.Services;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

/// <summary>Spec 043: patient insurance fields and the DRAFT claim export (not an official CMS-1500, not X12 837).</summary>
public class InsuranceClaimTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public InsuranceClaimTests(TestApiFactory f) => _f = f;

    private async Task<(AuthResult doctor, int pid)> SetupAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var doctor = await _f.RegisterDoctorAsync($"doc-{tag}@x.com");
        return (doctor, await _f.CreatePatientAsync(doctor.Token, $"pat-{tag}@x.com"));
    }

    private async Task<(AuthResult doctor, int pid, string email)> SetupWithEmailAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var doctor = await _f.RegisterDoctorAsync($"doc-{tag}@x.com");
        var email = $"pat-{tag}@x.com";
        return (doctor, await _f.CreatePatientAsync(doctor.Token, email), email);
    }

    private static object UpdateBody(object? extra = null)
    {
        var d = new Dictionary<string, object?>
        {
            ["firstName"] = "Pat", ["lastName"] = "Ient", ["dateOfBirth"] = "1990-01-01", ["gender"] = "Female",
            ["bloodType"] = "OPos", ["status"] = "Active", ["email"] = "pat@x.com", ["phone"] = "555-0000",
            ["address"] = "1 Main St", ["city"] = "Springfield", ["state"] = "IL", ["zipCode"] = "62701",
            ["insuranceProvider"] = "Acme Health", ["insurancePolicyNumber"] = "POL-1",
        };
        if (extra != null)
            foreach (var p in extra.GetType().GetProperties()) d[p.Name] = p.GetValue(extra);
        return d;
    }

    private Task<int> SeedInvoiceAsync(string doctorUserId, int pid, string description = "Consultation", decimal amount = 120.5m,
        int? appointmentId = null) =>
        _f.WithDbAsync(async db =>
        {
            var inv = new Invoice
            {
                PatientId = pid, DoctorId = doctorUserId, InvoiceNumber = $"INV-T{Guid.NewGuid().ToString("N")[..6]}",
                ServiceDescription = description, Amount = amount, AppointmentId = appointmentId,
                InvoiceDate = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc)
            };
            db.Invoices.Add(inv);
            await db.SaveChangesAsync();
            return inv.Id;
        });

    private Task<List<AuditEvent>> ViewsAsync(int pid, int invoiceId) =>
        _f.WithDbAsync(db => db.AuditEvents
            .Where(e => e.PatientId == pid && e.ItemKind == AuditItemKind.Invoice && e.ItemId == invoiceId && e.Action == AuditAction.View)
            .ToListAsync());

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static HttpRequestMessage Req(string url, string? lang = null)
    {
        var r = new HttpRequestMessage(HttpMethod.Get, url);
        if (lang != null) r.Headers.AcceptLanguage.ParseAdd(lang);
        return r;
    }

    private async Task<HttpResponseMessage> UpdateAsync(AuthResult d, int pid, object? extra, string? lang = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/patients/{pid}") { Content = JsonContent.Create(UpdateBody(extra)) };
        if (lang != null) req.Headers.AcceptLanguage.ParseAdd(lang);
        return await _f.ClientFor(d.Token).SendAsync(req);
    }

    private static readonly object FullInsurance = new
    {
        insuranceGroupNumber = "GRP-9", insurancePayerId = "PAY-77", insuranceSubscriberName = "Sam Holder",
        insuranceSubscriberDateOfBirth = "1980-05-06", insuranceSubscriberRelationship = "Spouse"
    };

    // ── Story 1: insurance fields on the patient ──────────────────────────────

    [Fact]
    public async Task Insurance_fields_are_saved_and_read_back_and_the_audit_lists_field_names_only()
    {
        var (d, pid) = await SetupAsync();

        var res = await UpdateAsync(d, pid, FullInsurance);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var p = await Json(await _f.ClientFor(d.Token).GetAsync($"/api/patients/{pid}"));
        Assert.Equal("GRP-9", p.GetProperty("insuranceGroupNumber").GetString());
        Assert.Equal("PAY-77", p.GetProperty("insurancePayerId").GetString());
        Assert.Equal("Sam Holder", p.GetProperty("insuranceSubscriberName").GetString());
        Assert.Equal("1980-05-06", p.GetProperty("insuranceSubscriberDateOfBirth").GetString());
        Assert.Equal("Spouse", p.GetProperty("insuranceSubscriberRelationship").GetString());

        var changes = await _f.WithDbAsync(db => db.AuditEvents
            .Where(e => e.PatientId == pid && e.Action == AuditAction.Change && e.ItemKind == AuditItemKind.Patient && e.ChangedFields != null)
            .ToListAsync());
        var fields = string.Join(",", changes.Select(c => c.ChangedFields));
        Assert.Contains("InsuranceGroupNumber", fields);
        Assert.Contains("InsuranceSubscriberRelationship", fields);
        Assert.DoesNotContain("Sam Holder", fields);
        Assert.DoesNotContain("GRP-9", fields);
    }

    [Fact]
    public async Task Patient_without_new_insurance_fields_reads_back_nulls()
    {
        var (d, pid) = await SetupAsync();
        var p = await Json(await _f.ClientFor(d.Token).GetAsync($"/api/patients/{pid}"));
        Assert.Equal(JsonValueKind.Null, p.GetProperty("insuranceGroupNumber").ValueKind);
        Assert.Equal(JsonValueKind.Null, p.GetProperty("insuranceSubscriberRelationship").ValueKind);
    }

    [Theory]
    [InlineData("insuranceGroupNumber", 101)]
    [InlineData("insurancePayerId", 51)]
    [InlineData("insuranceSubscriberName", 201)]
    public async Task Over_length_insurance_fields_are_rejected_in_both_languages_and_nothing_changes(string field, int length)
    {
        var (d, pid) = await SetupAsync();
        var body = (Dictionary<string, object?>)UpdateBody();
        body[field] = new string('x', length);

        foreach (var (lang, word) in new[] { ("es", "caracteres"), ("en", "characters") })
        {
            var req = new HttpRequestMessage(HttpMethod.Put, $"/api/patients/{pid}") { Content = JsonContent.Create(body) };
            req.Headers.AcceptLanguage.ParseAdd(lang);
            var res = await _f.ClientFor(d.Token).SendAsync(req);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Contains(word, await res.Content.ReadAsStringAsync());
        }
        var p = await Json(await _f.ClientFor(d.Token).GetAsync($"/api/patients/{pid}"));
        Assert.Equal(JsonValueKind.Null, p.GetProperty(field).ValueKind);
    }

    [Fact]
    public async Task Max_length_values_are_accepted()
    {
        var (d, pid) = await SetupAsync();
        var res = await UpdateAsync(d, pid, new { insuranceGroupNumber = new string('g', 100), insurancePayerId = new string('p', 50), insuranceSubscriberName = new string('n', 200) });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Future_subscriber_birth_date_and_unknown_relationship_are_rejected()
    {
        var (d, pid) = await SetupAsync();
        var future = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd");
        Assert.Equal(HttpStatusCode.BadRequest, (await UpdateAsync(d, pid, new { insuranceSubscriberDateOfBirth = future })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UpdateAsync(d, pid, new { insuranceSubscriberRelationship = "Cousin" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UpdateAsync(d, pid, new { insuranceSubscriberRelationship = 99 })).StatusCode);
    }

    [Fact]
    public async Task Create_accepts_and_validates_insurance_fields()
    {
        var d = await _f.RegisterDoctorAsync($"doc-{Guid.NewGuid():N}@x.com");
        object Body(string name) => new
        {
            firstName = "A", lastName = "B", dateOfBirth = "1990-01-01", gender = "Male", bloodType = "OPos", email = "a@b.com", phone = "555-0000",
            insuranceGroupNumber = "G1", insuranceSubscriberName = name, insuranceSubscriberRelationship = "Self"
        };
        var ok = await _f.ClientFor(d.Token).PostAsJsonAsync("/api/patients", Body("Sub"));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal("Self", (await Json(ok)).GetProperty("insuranceSubscriberRelationship").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await _f.ClientFor(d.Token).PostAsJsonAsync("/api/patients", Body(new string('x', 201)))).StatusCode);
    }

    [Fact]
    public async Task Other_doctor_cannot_update_insurance_of_a_patient_they_do_not_own()
    {
        var (owner, pid) = await SetupAsync();
        var other = await _f.RegisterDoctorAsync($"doc-{Guid.NewGuid():N}@x.com");
        Assert.Equal(HttpStatusCode.NotFound, (await UpdateAsync(other, pid, FullInsurance)).StatusCode);
        var p = await Json(await _f.ClientFor(owner.Token).GetAsync($"/api/patients/{pid}"));
        Assert.Equal(JsonValueKind.Null, p.GetProperty("insuranceGroupNumber").ValueKind);
    }

    // ── Story 2: draft claim export ───────────────────────────────────────────

    [Fact]
    public async Task Json_export_is_labelled_draft_not_837_and_has_items_and_missing_list()
    {
        var (d, pid) = await SetupAsync();
        await UpdateAsync(d, pid, FullInsurance);
        var id = await SeedInvoiceAsync(d.UserId, pid);

        var res = await _f.ClientFor(d.Token).SendAsync(Req($"/api/invoices/{id}/claim-export", "en"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/json", res.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-store", res.Headers.CacheControl!.ToString());
        var name = res.Content.Headers.ContentDisposition!.FileName!;
        Assert.StartsWith("claim-draft-INV-", name);
        Assert.EndsWith(".json", name);
        Assert.DoesNotContain("Ient", name);

        var j = await Json(res);
        Assert.Equal("DRAFT", j.GetProperty("status").GetString());
        var disclaimer = j.GetProperty("disclaimer").GetString()!;
        Assert.Contains("NOT the official CMS-1500 form", disclaimer);
        Assert.Contains("NOT an X12 837 file", disclaimer);
        Assert.Contains("NOT been validated", disclaimer);
        Assert.DoesNotContain("837-compliant", disclaimer, StringComparison.OrdinalIgnoreCase);

        string? Val(string key) => j.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("key").GetString() == key).GetProperty("value").GetString();
        Assert.Equal("Acme Health", Val("payer_name"));
        Assert.Equal("PAY-77", Val("payer_id"));
        Assert.Equal("POL-1", Val("insured_id_number"));
        Assert.Equal("Ient, Pat", Val("patient_name"));
        Assert.Equal("F", Val("patient_sex"));
        Assert.Equal("Spouse", Val("patient_relationship_to_insured"));
        Assert.Equal("Sam Holder", Val("insured_name"));
        Assert.Equal("1980-05-06", Val("insured_birth_date"));
        Assert.Equal("GRP-9", Val("insured_policy_group"));
        Assert.Equal("2026-09-01", Val("service_date"));
        Assert.Equal("120.50", Val("service_charge"));
        Assert.Equal("120.50", Val("total_charge"));

        var missing = j.GetProperty("missing").EnumerateArray().Select(m => m.GetProperty("key").GetString()).ToList();
        foreach (var k in new[] { "diagnosis_icd10", "procedure_cpt_hcpcs", "provider_npi", "federal_tax_id" }) Assert.Contains(k, missing);
        Assert.DoesNotContain("payer_name", missing);
        Assert.DoesNotContain("insured_id_number", missing);
    }

    [Fact]
    public async Task Export_for_uninsured_patient_still_succeeds_and_lists_the_gaps()
    {
        var (d, pid) = await SetupAsync();
        var id = await SeedInvoiceAsync(d.UserId, pid);

        var res = await _f.ClientFor(d.Token).GetAsync($"/api/invoices/{id}/claim-export");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var missing = (await Json(res)).GetProperty("missing").EnumerateArray().Select(m => m.GetProperty("key").GetString()).ToList();
        foreach (var k in new[] { "payer_name", "payer_id", "insured_id_number", "patient_relationship_to_insured", "insured_name", "insured_birth_date", "patient_address" })
            Assert.Contains(k, missing);
    }

    [Fact]
    public async Task Self_relationship_defaults_insured_to_the_patient()
    {
        var (d, pid) = await SetupAsync();
        await UpdateAsync(d, pid, new { insuranceSubscriberRelationship = "Self" });
        var id = await SeedInvoiceAsync(d.UserId, pid);

        var j = await Json(await _f.ClientFor(d.Token).GetAsync($"/api/invoices/{id}/claim-export"));
        string? Val(string key) => j.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("key").GetString() == key).GetProperty("value").GetString();
        Assert.Equal("Ient, Pat", Val("insured_name"));
        Assert.Equal("1990-01-01", Val("insured_birth_date"));
    }

    [Fact]
    public async Task Linked_appointment_date_is_the_service_date()
    {
        var (d, pid) = await SetupAsync();
        var apptId = await _f.WithDbAsync(async db =>
        {
            var a = new Appointment { PatientId = pid, DoctorId = d.UserId, ScheduledAt = new DateTime(2026, 8, 15, 9, 0, 0, DateTimeKind.Utc) };
            db.Appointments.Add(a);
            await db.SaveChangesAsync();
            return a.Id;
        });
        var id = await SeedInvoiceAsync(d.UserId, pid, appointmentId: apptId);

        var j = await Json(await _f.ClientFor(d.Token).GetAsync($"/api/invoices/{id}/claim-export"));
        Assert.Equal("2026-08-15", j.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("key").GetString() == "service_date").GetProperty("value").GetString());
    }

    [Fact]
    public async Task Csv_export_has_status_disclaimer_items_and_missing_rows()
    {
        var (d, pid) = await SetupAsync();
        await UpdateAsync(d, pid, FullInsurance);
        var id = await SeedInvoiceAsync(d.UserId, pid, description: "Visit, \"urgent\"\nsecond line");

        var res = await _f.ClientFor(d.Token).GetAsync($"/api/invoices/{id}/claim-export?format=CSV");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/csv", res.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith(".csv", res.Content.Headers.ContentDisposition!.FileName);
        Assert.Contains("no-store", res.Headers.CacheControl!.ToString());
        var csv = await res.Content.ReadAsStringAsync();
        Assert.StartsWith("section,item,key,label,value\r\n", csv);
        Assert.Contains("status,,status,,DRAFT", csv);
        Assert.Contains("X12 837", csv);
        Assert.Contains("item,1a,insured_id_number,", csv);
        Assert.Contains("missing,24D,procedure_cpt_hcpcs,", csv);
        Assert.Contains("\"Visit, \"\"urgent\"\"\nsecond line\"", csv);
    }

    [Theory]
    [InlineData("=cmd|' /C calc'!A0", "'=cmd|' /C calc'!A0")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("-2", "'-2")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData(null, "")]
    public void Csv_escape_quotes_and_neutralises_formulas(string? input, string expected) =>
        Assert.Equal(expected, ClaimCsvWriter.Escape(input));

    [Fact]
    public async Task Unsupported_format_is_a_localized_400()
    {
        var (d, pid) = await SetupAsync();
        var id = await SeedInvoiceAsync(d.UserId, pid);
        var es = await _f.ClientFor(d.Token).SendAsync(Req($"/api/invoices/{id}/claim-export?format=x12", "es"));
        var en = await _f.ClientFor(d.Token).SendAsync(Req($"/api/invoices/{id}/claim-export?format=837", "en"));
        Assert.Equal(HttpStatusCode.BadRequest, es.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, en.StatusCode);
        Assert.Contains("Formato no admitido", await es.Content.ReadAsStringAsync());
        Assert.Contains("Unsupported format", await en.Content.ReadAsStringAsync());
        Assert.Empty(await ViewsAsync(pid, id));
    }

    [Fact]
    public async Task Labels_and_disclaimer_follow_the_language_with_Spanish_default()
    {
        var (d, pid) = await SetupAsync();
        var id = await SeedInvoiceAsync(d.UserId, pid);
        var def = await Json(await _f.ClientFor(d.Token).GetAsync($"/api/invoices/{id}/claim-export"));
        var en = await Json(await _f.ClientFor(d.Token).SendAsync(Req($"/api/invoices/{id}/claim-export", "en")));
        Assert.Contains("NO es el formulario oficial", def.GetProperty("disclaimer").GetString());
        Assert.Contains("NOT the official", en.GetProperty("disclaimer").GetString());
        string Label(JsonElement j) => j.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("key").GetString() == "payer_name").GetProperty("label").GetString()!;
        Assert.Equal("Aseguradora (pagador)", Label(def));
        Assert.Equal("Payer (insurance carrier)", Label(en));
    }

    // ── Builder unit tests ────────────────────────────────────────────────────

    private static ClaimSourceData Source(Gender gender = Gender.Male, InsuranceRelationship? rel = null, string? subscriber = null,
        DateOnly? subscriberDob = null, DateTime? appt = null) =>
        new(1, 2, "INV-1", "Pending", "Visit", 10m, new DateTime(2026, 9, 1), appt,
            "Pat", "Ient", new DateOnly(1990, 1, 2), gender, "555", "1 Main", "City", "ST", "12345",
            "Acme", "POL", "GRP", "PAY", subscriber, subscriberDob, rel, "Dr. Doc Tor", "555-0100");

    [Fact]
    public void Builder_always_reports_the_codes_it_does_not_store_as_missing()
    {
        var missing = ClaimDraftBuilder.Build(Source(rel: InsuranceRelationship.Self)).Missing.Select(m => m.Key).ToList();
        foreach (var k in new[] { "diagnosis_icd10", "procedure_cpt_hcpcs", "provider_npi", "federal_tax_id" }) Assert.Contains(k, missing);
        Assert.DoesNotContain("insured_name", missing);
    }

    [Theory]
    [InlineData(Gender.Male, "M")]
    [InlineData(Gender.Female, "F")]
    [InlineData(Gender.NonBinary, null)]
    [InlineData(Gender.PreferNotToSay, null)]
    public void Builder_maps_sex_to_M_or_F_only(Gender g, string? expected)
    {
        var draft = ClaimDraftBuilder.Build(Source(g));
        Assert.Equal(expected, draft.Items.Single(i => i.Key == "patient_sex").Value);
        Assert.Equal(expected == null, draft.Missing.Any(m => m.Key == "patient_sex"));
    }

    [Fact]
    public void Builder_does_not_default_the_insured_when_relationship_is_not_self()
    {
        var draft = ClaimDraftBuilder.Build(Source(rel: InsuranceRelationship.Child));
        Assert.Contains(draft.Missing, m => m.Key == "insured_name");
        Assert.Contains(draft.Missing, m => m.Key == "insured_birth_date");
    }

    // ── Story 3: access control, audit, isolation ─────────────────────────────

    [Fact]
    public async Task Successful_export_is_audited_as_a_view_of_the_invoice_each_time()
    {
        var (d, pid) = await SetupAsync();
        var id = await SeedInvoiceAsync(d.UserId, pid);

        await _f.ClientFor(d.Token).GetAsync($"/api/invoices/{id}/claim-export");
        var view = Assert.Single(await ViewsAsync(pid, id));
        Assert.Equal(d.UserId, view.ActorUserId);
        Assert.Equal("Owner", view.ActorRole);
        Assert.Null(view.ChangedFields);

        await _f.ClientFor(d.Token).GetAsync($"/api/invoices/{id}/claim-export?format=csv");
        Assert.Equal(2, (await ViewsAsync(pid, id)).Count);
    }

    [Fact]
    public async Task Other_doctor_gets_404_identical_to_missing_and_nothing_is_audited()
    {
        var (owner, pid) = await SetupAsync();
        var id = await SeedInvoiceAsync(owner.UserId, pid);
        var other = await _f.RegisterDoctorAsync($"doc-{Guid.NewGuid():N}@x.com");
        var c = _f.ClientFor(other.Token);

        var foreign = await c.GetAsync($"/api/invoices/{id}/claim-export");
        var missing = await c.GetAsync("/api/invoices/99999999/claim-export");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        static string NoTrace(string b) => System.Text.RegularExpressions.Regex.Replace(b, "\"traceId\":\"[^\"]*\"", "");
        Assert.Equal(NoTrace(await missing.Content.ReadAsStringAsync()), NoTrace(await foreign.Content.ReadAsStringAsync()));
        Assert.Empty(await ViewsAsync(pid, id));
    }

    [Fact]
    public async Task Doctor_cannot_export_own_invoice_written_for_another_doctors_patient()
    {
        var (owner, pid) = await SetupAsync();
        var other = await _f.RegisterDoctorAsync($"doc-{Guid.NewGuid():N}@x.com");
        // Every record belongs to one clinic: an invoice issued by a doctor of another clinic for this patient cannot be saved
        await Assert.ThrowsAsync<InvalidOperationException>(() => SeedInvoiceAsync(other.UserId, pid));

        // ...and a normal invoice of the owner's clinic is invisible to the other clinic's doctor
        var id = await SeedInvoiceAsync(owner.UserId, pid);
        Assert.Equal(HttpStatusCode.NotFound, (await _f.ClientFor(other.Token).GetAsync($"/api/invoices/{id}/claim-export")).StatusCode);
        Assert.Empty(await ViewsAsync(pid, id));
    }

    [Fact]
    public async Task Patient_token_is_forbidden_and_anonymous_is_unauthorised()
    {
        var (d, pid, email) = await SetupWithEmailAsync();
        var id = await SeedInvoiceAsync(d.UserId, pid);
        var patient = await _f.OnboardPatientAsync(d.Token, pid, email);

        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(patient.Token).GetAsync($"/api/invoices/{id}/claim-export")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync($"/api/invoices/{id}/claim-export")).StatusCode);
        Assert.Empty(await ViewsAsync(pid, id));
    }

    [Fact]
    public async Task Deleted_patient_makes_the_export_a_404()
    {
        var (d, pid) = await SetupAsync();
        var id = await SeedInvoiceAsync(d.UserId, pid);
        Assert.Equal(HttpStatusCode.NoContent, (await _f.ClientFor(d.Token).DeleteAsync($"/api/patients/{pid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _f.ClientFor(d.Token).GetAsync($"/api/invoices/{id}/claim-export")).StatusCode);
    }

    [Fact]
    public async Task Portal_patient_dto_does_not_expose_new_insurance_fields()
    {
        var (d, pid, email) = await SetupWithEmailAsync();
        await UpdateAsync(d, pid, new
        {
            email, insuranceGroupNumber = "GRP-9", insurancePayerId = "PAY-77", insuranceSubscriberName = "Sam Holder",
            insuranceSubscriberDateOfBirth = "1980-05-06", insuranceSubscriberRelationship = "Spouse"
        });
        var patient = await _f.OnboardPatientAsync(d.Token, pid, email);
        var body = await (await _f.ClientFor(patient.Token).GetAsync("/api/portal/me")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("GRP-9", body);
        Assert.DoesNotContain("PAY-77", body);
        Assert.DoesNotContain("Sam Holder", body);
    }
}
