using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MedFlow.Core;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

/// <summary>Two clinics: nothing of one is visible or changeable from the other, and "other clinic" equals "missing".</summary>
public class ClinicIsolationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public ClinicIsolationTests(TestApiFactory f) => _f = f;

    private const int Missing = 987654;
    private static string Tag() => Guid.NewGuid().ToString("N")[..8];
    private static string Day(int offset) => DateTime.UtcNow.AddDays(offset).ToString("yyyy-MM-dd");

    private record World(AuthResult A, AuthResult B, AuthResult BDoctor, int Pid, int Appt, int Rx, int Inv, int Note, int Allergy,
        int Lab, int Attachment, int Waitlist);

    private static async Task<int> IdAsync(HttpResponseMessage res)
    {
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task<World> BuildAsync()
    {
        var a = await _f.RegisterDoctorAsync($"a-{Tag()}@x.com");
        var b = await _f.RegisterDoctorAsync($"b-{Tag()}@x.com");
        var bDoctor = await _f.JoinStaffAsync(b.Token, $"bd-{Tag()}@x.com", "Doctor");
        var ca = _f.ClientFor(a.Token);
        var pid = await _f.CreatePatientAsync(a.Token, $"p-{Tag()}@x.com");

        var appt = await IdAsync(await ca.PostAsJsonAsync("/api/appointments", new { patientId = pid, scheduledAt = DateTime.UtcNow.AddDays(4), durationMinutes = 30, type = "FollowUp" }));
        var rx = await IdAsync(await ca.PostAsJsonAsync("/api/prescriptions", new { patientId = pid, drugName = "Aspirin", dosage = "1", frequency = "d", issuedDate = Day(0), expiryDate = Day(20), refillsRemaining = 0 }));
        var inv = await IdAsync(await ca.PostAsJsonAsync("/api/invoices", new { patientId = pid, serviceDescription = "Visit", amount = 50 }));
        var note = await IdAsync(await ca.PostAsJsonAsync("/api/medicalnotes", new { patientId = pid, content = "Private note", visitType = "Checkup" }));
        (await ca.PostAsJsonAsync("/api/vitalsigns", new { patientId = pid, bloodPressure = "120/80" })).EnsureSuccessStatusCode();
        var allergy = await IdAsync(await ca.PostAsJsonAsync($"/api/patients/{pid}/clinical/allergies", new { substance = "Pollen", severity = "Mild" }));
        var lab = await IdAsync(await ca.PostAsJsonAsync($"/api/patients/{pid}/labs", new { testName = "CBC" }));
        var waitlist = await IdAsync(await ca.PostAsJsonAsync("/api/waitlist", new { patientId = pid }));

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 1, 2, 3, 4 });
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "scan.pdf");
        form.Add(new StringContent(pid.ToString()), "patientId");
        var attachment = await IdAsync(await ca.PostAsync("/api/attachments", form));

        return new World(a, b, bDoctor, pid, appt, rx, inv, note, allergy, lab, attachment, waitlist);
    }

    private static readonly Regex TraceId = new("\"traceId\":\"[^\"]*\"", RegexOptions.Compiled);

    private static async Task<(HttpStatusCode, string)> SendAsync(HttpClient c, HttpMethod method, string path, object? body)
    {
        var msg = new HttpRequestMessage(method, path);
        if (body != null) msg.Content = JsonContent.Create(body);
        var res = await c.SendAsync(msg);
        return (res.StatusCode, TraceId.Replace(await res.Content.ReadAsStringAsync(), "").Replace(Missing.ToString(), "{id}"));
    }

    private IEnumerable<(string Name, HttpMethod Method, string Real, string Gone, object? Body)> Cases(World w)
    {
        string P(string tpl, int real) => tpl.Replace("{pid}", w.Pid.ToString()).Replace("{id}", real.ToString());
        (string, HttpMethod, string, string, object?) C(string name, HttpMethod m, string tpl, int realId, object? body = null) =>
            (name, m, P(tpl, realId), tpl.Replace("{pid}", Missing.ToString()).Replace("{id}", Missing.ToString()), body);
        var upd = new { firstName = "X", lastName = "Y", dateOfBirth = "1990-01-01", gender = "Male", bloodType = "OPos", status = "Active", email = "x@x.com", phone = "1" };
        var visit = new { scheduledAt = DateTime.UtcNow.AddDays(6), durationMinutes = 30, type = "FollowUp", status = "Confirmed" };

        yield return C("patient get", HttpMethod.Get, "/api/patients/{pid}", 0);
        yield return C("patient put", HttpMethod.Put, "/api/patients/{pid}", 0, upd);
        yield return C("patient delete", HttpMethod.Delete, "/api/patients/{pid}", 0);
        yield return C("appointment get", HttpMethod.Get, "/api/appointments/{id}", w.Appt);
        yield return C("appointment put", HttpMethod.Put, "/api/appointments/{id}", w.Appt, visit);
        yield return C("appointment status", HttpMethod.Patch, "/api/appointments/{id}/status", w.Appt, "Cancelled");
        yield return C("appointment delete", HttpMethod.Delete, "/api/appointments/{id}", w.Appt);
        yield return C("appointment reminders", HttpMethod.Get, "/api/appointments/{id}/reminders", w.Appt);
        yield return C("appointments by patient", HttpMethod.Get, "/api/appointments/patient/{pid}", 0);
        yield return C("appointment create for patient", HttpMethod.Post, "/api/appointments", 0, new { patientId = Missing, scheduledAt = DateTime.UtcNow.AddDays(7), durationMinutes = 30, type = "FollowUp" });
        yield return C("prescription pdf", HttpMethod.Get, "/api/prescriptions/{id}/pdf", w.Rx);
        yield return C("prescription put", HttpMethod.Put, "/api/prescriptions/{id}", w.Rx, new { drugName = "X", dosage = "1", frequency = "d", expiryDate = Day(40), refillsRemaining = 1, status = "Active" });
        yield return C("prescription delete", HttpMethod.Delete, "/api/prescriptions/{id}", w.Rx);
        yield return C("prescriptions by patient", HttpMethod.Get, "/api/prescriptions/patient/{pid}", 0);
        yield return C("invoice claim", HttpMethod.Get, "/api/invoices/{id}/claim-export", w.Inv);
        yield return C("invoice put", HttpMethod.Put, "/api/invoices/{id}", w.Inv, new { serviceDescription = "X", amount = 1, status = "Pending" });
        yield return C("invoice paid", HttpMethod.Patch, "/api/invoices/{id}/mark-paid", w.Inv);
        yield return C("invoice delete", HttpMethod.Delete, "/api/invoices/{id}", w.Inv);
        yield return C("invoices by patient", HttpMethod.Get, "/api/invoices/patient/{pid}", 0);
        yield return C("vitals list", HttpMethod.Get, "/api/vitalsigns/patient/{pid}", 0);
        yield return C("vitals latest", HttpMethod.Get, "/api/vitalsigns/patient/{pid}/latest", 0);
        yield return C("vitals create", HttpMethod.Post, "/api/vitalsigns", 0, new { patientId = Missing, bloodPressure = "1/1" });
        yield return C("notes list", HttpMethod.Get, "/api/medicalnotes/patient/{pid}", 0);
        yield return C("notes latest", HttpMethod.Get, "/api/medicalnotes/patient/{pid}/latest", 0);
        yield return C("notes share", HttpMethod.Put, "/api/medicalnotes/{id}/sharing", w.Note, new { shared = true });
        yield return C("notes delete", HttpMethod.Delete, "/api/medicalnotes/{id}", w.Note);
        yield return C("allergy list", HttpMethod.Get, "/api/patients/{pid}/clinical", 0);
        yield return C("allergy add", HttpMethod.Post, "/api/patients/{pid}/clinical/allergies", 0, new { substance = "Dust", severity = "Mild" });
        yield return C("allergy put", HttpMethod.Put, "/api/patients/{pid}/clinical/allergies/{id}", w.Allergy, new { substance = "Dust2", severity = "Mild" });
        yield return C("allergy delete", HttpMethod.Delete, "/api/patients/{pid}/clinical/allergies/{id}", w.Allergy);
        yield return C("labs list", HttpMethod.Get, "/api/patients/{pid}/labs", 0);
        yield return C("labs add", HttpMethod.Post, "/api/patients/{pid}/labs", 0, new { testName = "X" });
        yield return C("lab cancel", HttpMethod.Post, "/api/patients/{pid}/labs/{id}/cancel", w.Lab);
        yield return C("lab delete", HttpMethod.Delete, "/api/patients/{pid}/labs/{id}", w.Lab);
        yield return C("attachments list", HttpMethod.Get, "/api/attachments/patient/{pid}", 0);
        yield return C("attachment download", HttpMethod.Get, "/api/attachments/{id}/download", w.Attachment);
        yield return C("attachment preview", HttpMethod.Get, "/api/attachments/{id}/preview", w.Attachment);
        yield return C("attachment share", HttpMethod.Put, "/api/attachments/{id}/sharing", w.Attachment, new { shared = true });
        yield return C("attachment delete", HttpMethod.Delete, "/api/attachments/{id}", w.Attachment);
        yield return C("waitlist add", HttpMethod.Post, "/api/waitlist", 0, new { patientId = Missing });
        yield return C("waitlist remove", HttpMethod.Delete, "/api/waitlist/{id}", w.Waitlist);
        yield return C("audit log", HttpMethod.Get, "/api/patients/{pid}/audit-log", 0);
        yield return C("portal invite", HttpMethod.Post, "/api/patients/{pid}/portal-invitation", 0);
        yield return C("portal revoke", HttpMethod.Delete, "/api/patients/{pid}/portal-access", 0);
        yield return C("intake link", HttpMethod.Post, "/api/patients/{pid}/intake-link", 0);
        yield return C("intake detail", HttpMethod.Get, "/api/intake-submissions/{id}", Missing);
        yield return C("intake accept", HttpMethod.Post, "/api/intake-submissions/{id}/accept", Missing);
    }

    [Fact]
    public async Task Another_clinics_record_answers_exactly_like_a_missing_record_for_every_area_and_role_with_access()
    {
        var w = await BuildAsync();
        var checkedCases = 0;
        foreach (var who in new[] { w.B, w.BDoctor })
        {
            var c = _f.ClientFor(who.Token);
            foreach (var (name, method, real, gone, body) in Cases(w))
            {
                var (statusReal, bodyReal) = await SendAsync(c, method, real, body);
                var (statusGone, bodyGone) = await SendAsync(c, method, gone, body);
                Assert.True(statusReal == HttpStatusCode.NotFound, $"{name}: expected 404 for the other clinic's record, got {(int)statusReal}");
                Assert.True(statusGone == statusReal, $"{name}: other-clinic {(int)statusReal} differs from missing {(int)statusGone}");
                Assert.True(bodyReal == bodyGone, $"{name}: other-clinic body differs from missing body");
                checkedCases++;
            }
        }
        Assert.True(checkedCases >= 90);

        // nothing was changed by those attempts
        var ca = _f.ClientFor(w.A.Token);
        Assert.Equal(HttpStatusCode.OK, (await ca.GetAsync($"/api/patients/{w.Pid}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ca.GetAsync($"/api/appointments/{w.Appt}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ca.GetAsync($"/api/prescriptions/{w.Rx}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ca.GetAsync($"/api/attachments/{w.Attachment}/download")).StatusCode);
        var notes = await ca.GetFromJsonAsync<JsonElement>($"/api/medicalnotes/patient/{w.Pid}");
        Assert.Single(notes.EnumerateArray());
        Assert.False(notes[0].GetProperty("sharedWithPatient").GetBoolean());
        var invoices = await ca.GetFromJsonAsync<JsonElement>($"/api/invoices/patient/{w.Pid}");
        Assert.Equal("Pending", invoices[0].GetProperty("status").GetString());
        var appt = await ca.GetFromJsonAsync<JsonElement>($"/api/appointments/{w.Appt}");
        Assert.Equal("Pending", appt.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Lists_and_dashboard_of_one_clinic_never_include_the_others_records()
    {
        var w = await BuildAsync();
        var b = _f.ClientFor(w.B.Token);
        foreach (var path in new[] { "/api/patients", "/api/appointments", "/api/prescriptions", "/api/invoices", "/api/waitlist" })
        {
            var list = await b.GetFromJsonAsync<JsonElement>(path);
            Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
        }
        var dash = await b.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.Equal(0, dash.GetProperty("totalPatients").GetInt32());
        Assert.Equal(0, dash.GetProperty("upcomingAppointments").GetInt32());
        Assert.Equal(0, dash.GetProperty("activePrescriptions").GetInt32());
        Assert.Equal(0, dash.GetProperty("pendingInvoicesAmount").GetDecimal());
        Assert.Empty(dash.GetProperty("recentPatients").EnumerateArray());
        Assert.Empty((await b.GetFromJsonAsync<JsonElement>("/api/appointments/upcoming")).EnumerateArray());
        Assert.Equal(0, (await b.GetFromJsonAsync<JsonElement>("/api/reports/revenue")).GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(0, (await b.GetFromJsonAsync<JsonElement>("/api/reports/ar-aging")).GetProperty("openInvoices").GetInt32());

        // and the owning clinic still sees its own
        var a = _f.ClientFor(w.A.Token);
        Assert.Equal(1, (await a.GetFromJsonAsync<JsonElement>("/api/patients")).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await a.GetFromJsonAsync<JsonElement>("/api/dashboard")).GetProperty("totalPatients").GetInt32());
    }

    [Fact]
    public async Task Clinic_scoped_ids_in_request_bodies_cannot_cross_clinics()
    {
        var w = await BuildAsync();
        var bPatient = await _f.CreatePatientAsync(w.B.Token, $"bp-{Tag()}@x.com");
        var cb = _f.ClientFor(w.B.Token);

        // B cannot attach its invoice to A's appointment, nor create records for A's patient
        Assert.Equal(HttpStatusCode.NotFound, (await cb.PostAsJsonAsync("/api/invoices", new { patientId = bPatient, appointmentId = w.Appt, serviceDescription = "x", amount = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cb.PostAsJsonAsync("/api/invoices", new { patientId = w.Pid, serviceDescription = "x", amount = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cb.PostAsJsonAsync("/api/prescriptions", new { patientId = w.Pid, drugName = "x", dosage = "1", frequency = "d", issuedDate = Day(0), expiryDate = Day(3), refillsRemaining = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cb.PostAsJsonAsync("/api/medicalnotes", new { patientId = w.Pid, content = "intrusion", visitType = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cb.PostAsJsonAsync("/api/appointments", new { patientId = w.Pid, scheduledAt = DateTime.UtcNow.AddDays(3), durationMinutes = 30, type = "FollowUp" })).StatusCode);
        // a doctor of clinic A cannot be named as the treating doctor in clinic B
        Assert.Equal(HttpStatusCode.NotFound, (await cb.PostAsJsonAsync("/api/appointments", new
        {
            patientId = bPatient, scheduledAt = DateTime.UtcNow.AddDays(3), durationMinutes = 30, type = "FollowUp", doctorId = w.A.UserId
        })).StatusCode);

        // nothing leaked into A
        var ca = _f.ClientFor(w.A.Token);
        Assert.Single((await ca.GetFromJsonAsync<JsonElement>($"/api/medicalnotes/patient/{w.Pid}")).EnumerateArray());
        Assert.Equal(1, (await ca.GetFromJsonAsync<JsonElement>("/api/invoices")).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Portal_patient_still_sees_only_their_own_data_after_the_clinic_change()
    {
        var a = await _f.RegisterDoctorAsync($"pa-{Tag()}@x.com");
        var email = $"portal-{Tag()}@x.com";
        var pid = await _f.CreatePatientAsync(a.Token, email);
        var other = await _f.CreatePatientAsync(a.Token, $"other-{Tag()}@x.com", "Other");
        await _f.SeedClinicalDataAsync(pid, a.UserId, "MINE");
        await _f.SeedClinicalDataAsync(other, a.UserId, "NOTMINE");
        var patient = await _f.OnboardPatientAsync(a.Token, pid, email);
        var c = _f.ClientFor(patient.Token);

        var rx = await c.GetFromJsonAsync<JsonElement>("/api/portal/prescriptions");
        Assert.Single(rx.EnumerateArray());
        Assert.Equal("MINE", rx[0].GetProperty("drugName").GetString());
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/portal/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/portal/appointments")).StatusCode);
        // the portal token is not a staff token even for the same clinic
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/clinic")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/staff")).StatusCode);
    }

    // ── ClinicId stamping and integrity ───────────────────────────────────────

    [Fact]
    public async Task Rows_saved_without_a_clinic_take_it_from_their_patient()
    {
        var a = await _f.RegisterDoctorAsync($"st-{Tag()}@x.com");
        var pid = await _f.CreatePatientAsync(a.Token, $"p-{Tag()}@x.com");
        var clinicId = await _f.WithDbAsync(db => db.ClinicMembers.Where(m => m.UserId == a.UserId).Select(m => m.ClinicId).SingleAsync());

        var stamped = await _f.WithDbAsync(async db =>
        {
            var appt = new Appointment { PatientId = pid, DoctorId = a.UserId, ScheduledAt = DateTime.UtcNow.AddDays(1) };
            var vital = new VitalSign { PatientId = pid };
            var allergy = new PatientAllergy { PatientId = pid, DoctorId = a.UserId, Substance = "S" };
            var audit = new AuditEvent { PatientId = pid, DoctorId = a.UserId, ActorUserId = a.UserId, ActorName = "x", ActorRole = "Owner" };
            db.AddRange(appt, vital, allergy, audit);
            await db.SaveChangesAsync();
            return new[] { appt.ClinicId, vital.ClinicId, allergy.ClinicId, audit.ClinicId };
        });
        Assert.All(stamped, id => Assert.Equal(clinicId, id));
        Assert.Equal(clinicId, (await _f.WithDbAsync(db => db.Patients.AsNoTracking().SingleAsync(p => p.Id == pid))).ClinicId);
    }

    [Fact]
    public async Task Saving_a_record_that_contradicts_its_clinic_or_has_none_is_refused()
    {
        var a = await _f.RegisterDoctorAsync($"ix-{Tag()}@x.com");
        var b = await _f.RegisterDoctorAsync($"iy-{Tag()}@x.com");
        var pid = await _f.CreatePatientAsync(a.Token, $"p-{Tag()}@x.com");
        var bClinic = await _f.WithDbAsync(db => db.ClinicMembers.Where(m => m.UserId == b.UserId).Select(m => m.ClinicId).SingleAsync());

        Task<int> Save(Func<Core.Entities.IClinicScoped> make) => _f.WithDbAsync(async db =>
        {
            db.Add(make());
            return await db.SaveChangesAsync();
        });

        // explicit clinic different from the patient's
        await Assert.ThrowsAsync<InvalidOperationException>(() => Save(() => new Appointment { PatientId = pid, DoctorId = a.UserId, ClinicId = bClinic, ScheduledAt = DateTime.UtcNow }));
        // a doctor of another clinic as author
        await Assert.ThrowsAsync<InvalidOperationException>(() => Save(() => new Prescription { PatientId = pid, DoctorId = b.UserId, DrugName = "x", Dosage = "1", Frequency = "d" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Save(() => new MedicalNote { PatientId = pid, DoctorId = b.UserId, Content = "x" }));
        // a patient whose doctor belongs to no clinic
        await Assert.ThrowsAsync<InvalidOperationException>(() => Save(() => new Patient { FirstName = "n", LastName = "m", DoctorId = "nobody" }));
        // a row for a patient that does not exist cannot be given a clinic
        await Assert.ThrowsAsync<InvalidOperationException>(() => Save(() => new VitalSign { PatientId = 424242 }));
    }

    [Fact]
    public async Task Every_clinic_scoped_entity_has_an_indexed_foreign_key_to_clinics()
    {
        await _f.WithDbAsync(db =>
        {
            var scoped = db.Model.GetEntityTypes().Where(t => typeof(IClinicScoped).IsAssignableFrom(t.ClrType)).ToList();
            Assert.True(scoped.Count >= 14, $"found {scoped.Count}");
            foreach (var t in scoped)
            {
                Assert.Contains(t.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(Clinic) && fk.Properties.Single().Name == "ClinicId");
                Assert.Contains(t.GetIndexes(), ix => ix.Properties.Count == 1 && ix.Properties[0].Name == "ClinicId");
            }
            return Task.FromResult(0);
        });
    }
}
