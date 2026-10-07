using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedFlow.Core;
using MedFlow.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

public record ClinicWorld(AuthResult Owner, AuthResult Doctor, AuthResult Nurse, AuthResult Receptionist,
    int PatientId, int NoteId, string PatientEmail)
{
    public AuthResult For(ClinicRole role) => role switch
    {
        ClinicRole.Owner => Owner,
        ClinicRole.Doctor => Doctor,
        ClinicRole.Nurse => Nurse,
        _ => Receptionist
    };
}

/// <summary>One clinic with an owner, a second doctor, a nurse and a receptionist, built once per test class.</summary>
public class ClinicFixture : TestApiFactory
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ClinicWorld? _world;

    public async Task<ClinicWorld> WorldAsync()
    {
        await _gate.WaitAsync();
        try { return _world ??= await BuildAsync(); }
        finally { _gate.Release(); }
    }

    public async Task<ClinicWorld> BuildAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var owner = await RegisterDoctorAsync($"owner-{tag}@x.com");
        var doctor = await JoinStaffAsync(owner.Token, $"doc2-{tag}@x.com", "Doctor", "Dora", "Doctor");
        var nurse = await JoinStaffAsync(owner.Token, $"nurse-{tag}@x.com", "Nurse", "Nina", "Nurse");
        var reception = await JoinStaffAsync(owner.Token, $"recep-{tag}@x.com", "Receptionist", "Rita", "Reception");
        var email = $"pat-{tag}@x.com";
        var pid = await CreatePatientAsync(owner.Token, email);
        var note = await ClientFor(owner.Token).PostAsJsonAsync("/api/medicalnotes",
            new { patientId = pid, content = "Initial note", visitType = "Checkup" });
        note.EnsureSuccessStatusCode();
        var noteId = (await note.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        return new ClinicWorld(owner, doctor, nurse, reception, pid, noteId, email);
    }
}

public class ClinicRolesTests : IClassFixture<ClinicFixture>
{
    private readonly ClinicFixture _f;
    public ClinicRolesTests(ClinicFixture f) => _f = f;

    private record Req(HttpMethod Method, string Path, object? Body = null);

    private static Req Get(string path) => new(HttpMethod.Get, path);
    private static string Day(int offset) => DateTime.UtcNow.AddDays(offset).ToString("yyyy-MM-dd");

    // One representative request per permission. Field-level PatientClinicalFields is covered by the redaction tests.
    private static readonly Dictionary<Permission, Func<ClinicFixture, ClinicWorld, Task<Req>>> Cases = new()
    {
        [Permission.ClinicRead] = (_, _) => Task.FromResult(Get("/api/clinic")),
        [Permission.DashboardRead] = (_, _) => Task.FromResult(Get("/api/dashboard")),
        [Permission.PatientsRead] = (_, w) => Task.FromResult(Get($"/api/patients/{w.PatientId}")),
        [Permission.PatientsWrite] = (_, _) => Task.FromResult(new Req(HttpMethod.Post, "/api/patients", new
        {
            firstName = "New", lastName = "Patient", dateOfBirth = "1991-02-03", gender = "Female", bloodType = "OPos",
            email = $"new-{Guid.NewGuid():N}@x.com", phone = "555-1111"
        })),
        [Permission.PatientsDelete] = async (f, w) =>
        {
            var id = await f.CreatePatientAsync(w.Owner.Token, $"del-{Guid.NewGuid():N}@x.com");
            return new Req(HttpMethod.Delete, $"/api/patients/{id}");
        },
        [Permission.AppointmentsRead] = (_, _) => Task.FromResult(Get("/api/appointments")),
        [Permission.AppointmentsWrite] = (_, w) => Task.FromResult(new Req(HttpMethod.Post, "/api/appointments", new
        {
            patientId = w.PatientId, scheduledAt = DateTime.UtcNow.AddDays(5), durationMinutes = 30, type = "FollowUp"
        })),
        [Permission.WaitlistManage] = (_, _) => Task.FromResult(Get("/api/waitlist")),
        [Permission.AvailabilityManage] = (_, _) => Task.FromResult(Get("/api/availability")),
        [Permission.IntakeLinkSend] = (_, w) => Task.FromResult(new Req(HttpMethod.Post, $"/api/patients/{w.PatientId}/intake-link")),
        [Permission.IntakeReview] = (_, _) => Task.FromResult(Get("/api/intake-submissions")),
        [Permission.PortalInvite] = (_, w) => Task.FromResult(new Req(HttpMethod.Post, $"/api/patients/{w.PatientId}/portal-invitation")),
        [Permission.PrescriptionsRead] = (_, _) => Task.FromResult(Get("/api/prescriptions")),
        [Permission.PrescriptionsWrite] = (_, w) => Task.FromResult(new Req(HttpMethod.Post, "/api/prescriptions", new
        {
            patientId = w.PatientId, drugName = "Drug", dosage = "1", frequency = "daily",
            issuedDate = Day(0), expiryDate = Day(30), refillsRemaining = 1
        })),
        [Permission.InvoicesRead] = (_, _) => Task.FromResult(Get("/api/invoices")),
        [Permission.InvoicesWrite] = (_, w) => Task.FromResult(new Req(HttpMethod.Post, "/api/invoices", new
        {
            patientId = w.PatientId, serviceDescription = "Visit", amount = 25
        })),
        [Permission.VitalsRead] = (_, w) => Task.FromResult(Get($"/api/vitalsigns/patient/{w.PatientId}")),
        [Permission.VitalsWrite] = (_, w) => Task.FromResult(new Req(HttpMethod.Post, "/api/vitalsigns",
            new { patientId = w.PatientId, bloodPressure = "120/80", heartRate = 70 })),
        [Permission.NotesRead] = (_, w) => Task.FromResult(Get($"/api/medicalnotes/patient/{w.PatientId}")),
        [Permission.NotesWrite] = (_, w) => Task.FromResult(new Req(HttpMethod.Post, "/api/medicalnotes",
            new { patientId = w.PatientId, content = "Another note", visitType = "Checkup" })),
        [Permission.NotesManage] = (_, w) => Task.FromResult(new Req(HttpMethod.Put, $"/api/medicalnotes/{w.NoteId}/sharing", new { shared = false })),
        [Permission.NoteTemplates] = (_, _) => Task.FromResult(Get("/api/notetemplates")),
        [Permission.AttachmentsRead] = (_, w) => Task.FromResult(Get($"/api/attachments/patient/{w.PatientId}")),
        [Permission.AttachmentsWrite] = (_, _) => Task.FromResult(new Req(HttpMethod.Delete, "/api/attachments/999999")),
        [Permission.ClinicalListsRead] = (_, w) => Task.FromResult(Get($"/api/patients/{w.PatientId}/clinical")),
        [Permission.ClinicalListsWrite] = (_, w) => Task.FromResult(new Req(HttpMethod.Post, $"/api/patients/{w.PatientId}/clinical/allergies",
            new { substance = $"Dust-{Guid.NewGuid():N}", severity = "Mild" })),
        [Permission.LabsRead] = (_, w) => Task.FromResult(Get($"/api/patients/{w.PatientId}/labs")),
        [Permission.LabsWrite] = (_, w) => Task.FromResult(new Req(HttpMethod.Post, $"/api/patients/{w.PatientId}/labs",
            new { testName = "CBC" })),
        [Permission.AuditLogRead] = (_, w) => Task.FromResult(Get($"/api/patients/{w.PatientId}/audit-log")),
        [Permission.ReportsRead] = (_, _) => Task.FromResult(Get("/api/reports/revenue")),
        [Permission.StaffManage] = (_, _) => Task.FromResult(Get("/api/staff")),
    };

    public static IEnumerable<object[]> MatrixCases() =>
        Cases.Keys.SelectMany(p => Enum.GetValues<ClinicRole>().Select(r => new object[] { p, r }));

    [Fact]
    public void Matrix_table_covers_every_permission_except_the_field_level_one()
    {
        var expected = Enum.GetValues<Permission>().Where(p => p != Permission.PatientClinicalFields).ToHashSet();
        Assert.True(expected.SetEquals(Cases.Keys));
    }

    [Theory]
    [MemberData(nameof(MatrixCases))]
    public async Task Role_by_area_allow_deny_follows_the_permission_matrix(Permission permission, ClinicRole role)
    {
        var w = await _f.WorldAsync();
        var req = await Cases[permission](_f, w);
        var msg = new HttpRequestMessage(req.Method, req.Path);
        if (req.Body != null) msg.Content = JsonContent.Create(req.Body);

        var res = await _f.ClientFor(w.For(role).Token).SendAsync(msg);

        if (PermissionMatrix.Has(role, permission))
        {
            Assert.NotEqual(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.NotEqual(HttpStatusCode.Unauthorized, res.StatusCode);
            Assert.True((int)res.StatusCode < 500, $"{role} {permission} -> {(int)res.StatusCode}");
        }
        else Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [MemberData(nameof(MatrixCases))]
    public async Task Patient_portal_token_never_satisfies_any_staff_permission(Permission permission, ClinicRole _)
    {
        var w = await _f.WorldAsync();
        var email = $"portal-{Guid.NewGuid():N}@x.com";
        var pid = await _f.CreatePatientAsync(w.Owner.Token, email);
        var patient = await _f.OnboardPatientAsync(w.Owner.Token, pid, email);
        var req = await Cases[permission](_f, w);
        var msg = new HttpRequestMessage(req.Method, req.Path);
        if (req.Body != null) msg.Content = JsonContent.Create(req.Body);

        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(patient.Token).SendAsync(msg)).StatusCode);
    }

    [Theory]
    [MemberData(nameof(MatrixCases))]
    public async Task Anonymous_is_unauthorized_on_every_staff_endpoint(Permission permission, ClinicRole _)
    {
        var w = await _f.WorldAsync();
        var req = await Cases[permission](_f, w);
        var msg = new HttpRequestMessage(req.Method, req.Path);
        if (req.Body != null) msg.Content = JsonContent.Create(req.Body);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().SendAsync(msg)).StatusCode);
    }

    [Fact]
    public void Matrix_encodes_the_documented_role_summaries()
    {
        // Receptionist: no clinical data at all
        foreach (var p in new[] { Permission.NotesRead, Permission.NotesWrite, Permission.LabsRead, Permission.PrescriptionsRead,
                     Permission.ClinicalListsRead, Permission.VitalsRead, Permission.AttachmentsRead, Permission.PatientClinicalFields })
            Assert.False(PermissionMatrix.Has(ClinicRole.Receptionist, p), p.ToString());
        // Nurse: clinical read plus vitals and notes entry, but no prescribing, invoicing or staff management
        Assert.True(PermissionMatrix.Has(ClinicRole.Nurse, Permission.VitalsWrite));
        Assert.True(PermissionMatrix.Has(ClinicRole.Nurse, Permission.NotesWrite));
        foreach (var p in new[] { Permission.PrescriptionsWrite, Permission.InvoicesRead, Permission.InvoicesWrite, Permission.StaffManage })
            Assert.False(PermissionMatrix.Has(ClinicRole.Nurse, p), p.ToString());
        // Only Owners manage staff; Owners hold every permission
        Assert.Equal(new[] { ClinicRole.Owner }, Enum.GetValues<ClinicRole>().Where(r => PermissionMatrix.Has(r, Permission.StaffManage)));
        Assert.All(Enum.GetValues<Permission>(), p => Assert.True(PermissionMatrix.Has(ClinicRole.Owner, p)));
        Assert.DoesNotContain(Permission.StaffManage, PermissionMatrix.For(ClinicRole.Doctor));
    }

    // ── Receptionist data redaction ───────────────────────────────────────────

    private async Task<int> ClinicalPatientAsync(ClinicWorld w, string tag)
    {
        var res = await _f.ClientFor(w.Owner.Token).PostAsJsonAsync("/api/patients", new
        {
            firstName = "Clin", lastName = tag, dateOfBirth = "1980-05-05", gender = "Male", bloodType = "ABPos",
            email = $"{tag}@x.com", phone = "555-2222", primaryCondition = $"Hypertension-{tag}",
            allergies = $"Peanuts-{tag}", notes = $"SecretNote-{tag}"
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Receptionist_gets_demographics_but_not_clinical_fields_nurse_and_doctor_do()
    {
        var w = await _f.WorldAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var pid = await ClinicalPatientAsync(w, tag);

        var rec = await _f.ClientFor(w.Receptionist.Token).GetFromJsonAsync<JsonElement>($"/api/patients/{pid}");
        Assert.Equal("Clin", rec.GetProperty("firstName").GetString());
        Assert.Equal($"{tag}@x.com", rec.GetProperty("email").GetString());
        Assert.Equal(JsonValueKind.Null, rec.GetProperty("primaryCondition").ValueKind);
        Assert.Equal(JsonValueKind.Null, rec.GetProperty("allergies").ValueKind);
        Assert.Equal(JsonValueKind.Null, rec.GetProperty("notes").ValueKind);
        Assert.Equal("Unknown", rec.GetProperty("bloodType").GetString());

        foreach (var clinician in new[] { w.Nurse, w.Doctor, w.Owner })
        {
            var full = await _f.ClientFor(clinician.Token).GetFromJsonAsync<JsonElement>($"/api/patients/{pid}");
            Assert.Equal($"Hypertension-{tag}", full.GetProperty("primaryCondition").GetString());
            Assert.Equal($"Peanuts-{tag}", full.GetProperty("allergies").GetString());
            Assert.Equal($"SecretNote-{tag}", full.GetProperty("notes").GetString());
            Assert.Equal("ABPos", full.GetProperty("bloodType").GetString());
        }
    }

    [Fact]
    public async Task Receptionist_list_and_search_do_not_expose_or_match_clinical_text()
    {
        var w = await _f.WorldAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        await ClinicalPatientAsync(w, tag);

        var listed = await _f.ClientFor(w.Receptionist.Token).GetFromJsonAsync<JsonElement>($"/api/patients?search=Hypertension-{tag}");
        Assert.Equal(0, listed.GetProperty("totalCount").GetInt32()); // cannot infer the withheld condition by searching it

        var byName = await _f.ClientFor(w.Receptionist.Token).GetFromJsonAsync<JsonElement>($"/api/patients?search={tag}");
        var item = byName.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, item.GetProperty("primaryCondition").ValueKind);
        Assert.Equal("Unknown", item.GetProperty("bloodType").GetString());

        var asDoctor = await _f.ClientFor(w.Doctor.Token).GetFromJsonAsync<JsonElement>($"/api/patients?search=Hypertension-{tag}");
        Assert.Equal(1, asDoctor.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Receptionist_edit_updates_demographics_and_preserves_clinical_fields()
    {
        var w = await _f.WorldAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var pid = await ClinicalPatientAsync(w, tag);

        var res = await _f.ClientFor(w.Receptionist.Token).PutAsJsonAsync($"/api/patients/{pid}", new
        {
            firstName = "Clin", lastName = tag, dateOfBirth = "1980-05-05", gender = "Male", bloodType = "ANeg", status = "Active",
            email = $"{tag}@x.com", phone = "555-9999", primaryCondition = "OVERWRITE", allergies = "OVERWRITE", notes = "OVERWRITE"
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var after = await _f.ClientFor(w.Owner.Token).GetFromJsonAsync<JsonElement>($"/api/patients/{pid}");
        Assert.Equal("555-9999", after.GetProperty("phone").GetString());
        Assert.Equal($"Hypertension-{tag}", after.GetProperty("primaryCondition").GetString());
        Assert.Equal($"Peanuts-{tag}", after.GetProperty("allergies").GetString());
        Assert.Equal($"SecretNote-{tag}", after.GetProperty("notes").GetString());
        Assert.Equal("ABPos", after.GetProperty("bloodType").GetString());
    }

    [Fact]
    public async Task Receptionist_created_patient_drops_clinical_fields_and_gets_a_clinic_doctor()
    {
        var w = await _f.WorldAsync();
        var email = $"rc-{Guid.NewGuid():N}@x.com";
        var res = await _f.ClientFor(w.Receptionist.Token).PostAsJsonAsync("/api/patients", new
        {
            firstName = "Walk", lastName = "In", dateOfBirth = "1999-01-01", gender = "Female", bloodType = "BPos",
            email, phone = "555-3333", primaryCondition = "ignored", allergies = "ignored", notes = "ignored"
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var id = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var stored = await _f.WithDbAsync(db => db.Patients.AsNoTracking().FirstAsync(p => p.Id == id));
        Assert.Null(stored.PrimaryCondition);
        Assert.Null(stored.Allergies);
        Assert.Null(stored.Notes);
        Assert.Equal(BloodTypeUnknown, stored.BloodType.ToString());
        Assert.Equal(w.Owner.UserId, stored.DoctorId); // no doctor requested: the clinic's Owner treats the patient
        var ownerClinic = await _f.WithDbAsync(db => db.ClinicMembers.Where(m => m.UserId == w.Owner.UserId).Select(m => m.ClinicId).FirstAsync());
        Assert.Equal(ownerClinic, stored.ClinicId);
    }

    private const string BloodTypeUnknown = "Unknown";

    [Fact]
    public async Task Receptionist_can_choose_a_clinic_doctor_but_not_a_foreign_or_non_doctor_user()
    {
        var w = await _f.WorldAsync();
        var body = (string? doctorId) => new
        {
            firstName = "Pick", lastName = "Doc", dateOfBirth = "1999-01-01", gender = "Female", bloodType = "BPos",
            email = $"pd-{Guid.NewGuid():N}@x.com", phone = "555-3333", doctorId
        };
        var c = _f.ClientFor(w.Receptionist.Token);

        var ok = await c.PostAsJsonAsync("/api/patients", body(w.Doctor.UserId));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var id = (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(w.Doctor.UserId, (await _f.WithDbAsync(db => db.Patients.AsNoTracking().FirstAsync(p => p.Id == id))).DoctorId);

        var foreign = await _f.RegisterDoctorAsync($"foreign-{Guid.NewGuid():N}@x.com");
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync("/api/patients", body(foreign.UserId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync("/api/patients", body(w.Nurse.UserId))).StatusCode);

        var appt = (string? doctorId) => new
        {
            patientId = w.PatientId, scheduledAt = DateTime.UtcNow.AddDays(9), durationMinutes = 30, type = "FollowUp", doctorId
        };
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/appointments", appt(w.Doctor.UserId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync("/api/appointments", appt(foreign.UserId))).StatusCode);
    }

    // ── Nurse ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Nurse_records_vitals_and_notes_and_the_note_shows_the_nurse_as_author()
    {
        var w = await _f.WorldAsync();
        var c = _f.ClientFor(w.Nurse.Token);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/vitalsigns", new { patientId = w.PatientId, bloodPressure = "118/76", heartRate = 66 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/medicalnotes", new { patientId = w.PatientId, content = "Nurse observation", visitType = "Triage" })).StatusCode);

        var notes = await _f.ClientFor(w.Owner.Token).GetFromJsonAsync<JsonElement>($"/api/medicalnotes/patient/{w.PatientId}");
        var mine = notes.EnumerateArray().Single(n => n.GetProperty("content").GetString() == "Nurse observation");
        Assert.Equal("Nina Nurse", mine.GetProperty("doctorName").GetString()); // not "Dr. ..."

        var stored = await _f.WithDbAsync(db => db.MedicalNotes.AsNoTracking().FirstAsync(n => n.Content == "Nurse observation"));
        Assert.Equal(w.Nurse.UserId, stored.DoctorId);
        var vitals = await c.GetFromJsonAsync<JsonElement>($"/api/vitalsigns/patient/{w.PatientId}");
        Assert.Contains(vitals.EnumerateArray(), v => v.GetProperty("recordedBy").GetString() == w.Nurse.UserId);
    }

    [Fact]
    public async Task Nurse_cannot_prescribe_invoice_share_notes_or_manage_staff()
    {
        var w = await _f.WorldAsync();
        var c = _f.ClientFor(w.Nurse.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/prescriptions", new
        {
            patientId = w.PatientId, drugName = "X", dosage = "1", frequency = "d", issuedDate = Day(0), expiryDate = Day(10), refillsRemaining = 0
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/invoices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync($"/api/medicalnotes/{w.NoteId}/sharing", new { shared = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/staff/invitations", new { email = "x@x.com", role = "Nurse" })).StatusCode);
    }

    [Fact]
    public async Task Dashboard_zeroes_figures_for_areas_the_role_cannot_open()
    {
        var w = await _f.WorldAsync();
        var inv = await _f.ClientFor(w.Owner.Token).PostAsJsonAsync("/api/invoices", new { patientId = w.PatientId, serviceDescription = "D", amount = 40 });
        inv.EnsureSuccessStatusCode();
        await _f.ClientFor(w.Owner.Token).PostAsJsonAsync("/api/prescriptions", new
        {
            patientId = w.PatientId, drugName = "D", dosage = "1", frequency = "d", issuedDate = Day(0), expiryDate = Day(90), refillsRemaining = 0
        });

        var owner = await _f.ClientFor(w.Owner.Token).GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.True(owner.GetProperty("pendingInvoicesAmount").GetDecimal() >= 40);
        Assert.True(owner.GetProperty("activePrescriptions").GetInt32() >= 1);

        var nurse = await _f.ClientFor(w.Nurse.Token).GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.Equal(0, nurse.GetProperty("pendingInvoicesAmount").GetDecimal());
        Assert.Equal(0, nurse.GetProperty("overdueInvoices").GetInt32());
        Assert.True(nurse.GetProperty("activePrescriptions").GetInt32() >= 1);

        var rec = await _f.ClientFor(w.Receptionist.Token).GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.Equal(0, rec.GetProperty("activePrescriptions").GetInt32());
        Assert.True(rec.GetProperty("pendingInvoicesAmount").GetDecimal() >= 40);
        Assert.All(rec.GetProperty("recentPatients").EnumerateArray(),
            p => Assert.Equal(JsonValueKind.Null, p.GetProperty("primaryCondition").ValueKind));
    }

    // ── Doctor, invoices, appointments ────────────────────────────────────────

    [Fact]
    public async Task Second_doctor_has_full_clinical_access_to_a_colleagues_patient()
    {
        var w = await _f.WorldAsync();
        var c = _f.ClientFor(w.Doctor.Token);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/patients/{w.PatientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/medicalnotes", new { patientId = w.PatientId, content = "Second doctor", visitType = "Checkup" })).StatusCode);
        var rx = await c.PostAsJsonAsync("/api/prescriptions", new
        {
            patientId = w.PatientId, drugName = "ColleagueRx", dosage = "1", frequency = "d", issuedDate = Day(0), expiryDate = Day(10), refillsRemaining = 0
        });
        Assert.Equal(HttpStatusCode.Created, rx.StatusCode);
        var id = (await rx.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        // the owner sees the colleague's prescription and can print it
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(w.Owner.Token).GetAsync($"/api/prescriptions/{id}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/medicalnotes/patient/{w.PatientId}/latest")).StatusCode);
    }

    [Fact]
    public async Task Receptionist_invoice_is_billed_by_the_patients_doctor_and_can_be_paid()
    {
        var w = await _f.WorldAsync();
        var c = _f.ClientFor(w.Receptionist.Token);
        var res = await c.PostAsJsonAsync("/api/invoices", new { patientId = w.PatientId, serviceDescription = "Consult", amount = 60 });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var id = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await c.PatchAsync($"/api/invoices/{id}/mark-paid", null)).StatusCode);

        var stored = await _f.WithDbAsync(db => db.Invoices.AsNoTracking().FirstAsync(i => i.Id == id));
        Assert.Equal(w.Owner.UserId, stored.DoctorId); // the patient's treating doctor, not the receptionist
        Assert.Equal(InvoiceStatusPaid, stored.Status.ToString());
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/invoices/{id}/claim-export")).StatusCode);
    }

    private const string InvoiceStatusPaid = "Paid";

    [Fact]
    public async Task Invoice_cannot_link_an_appointment_of_another_patient_or_clinic()
    {
        var w = await _f.WorldAsync();
        var foreign = await _f.RegisterDoctorAsync($"fx-{Guid.NewGuid():N}@x.com");
        var fp = await _f.CreatePatientAsync(foreign.Token, $"fp-{Guid.NewGuid():N}@x.com");
        var fa = await _f.ClientFor(foreign.Token).PostAsJsonAsync("/api/appointments", new
        {
            patientId = fp, scheduledAt = DateTime.UtcNow.AddDays(3), durationMinutes = 30, type = "FollowUp"
        });
        var apptId = (await fa.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var res = await _f.ClientFor(w.Owner.Token).PostAsJsonAsync("/api/invoices",
            new { patientId = w.PatientId, appointmentId = apptId, serviceDescription = "X", amount = 5 });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── Audit ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Audit_events_record_the_staff_role_and_only_owner_and_treating_doctor_read_them()
    {
        var w = await _f.WorldAsync();
        var pid = await _f.CreatePatientAsync(w.Owner.Token, $"au-{Guid.NewGuid():N}@x.com");

        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(w.Nurse.Token).GetAsync($"/api/patients/{pid}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(w.Receptionist.Token).GetAsync($"/api/patients/{pid}")).StatusCode);

        var ownerLog = await _f.ClientFor(w.Owner.Token).GetFromJsonAsync<JsonElement>($"/api/patients/{pid}/audit-log?pageSize=100");
        var events = ownerLog.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(events, e => e.GetProperty("actorRole").GetString() == "Nurse" && e.GetProperty("actorName").GetString() == "Nina Nurse");
        Assert.Contains(events, e => e.GetProperty("actorRole").GetString() == "Receptionist");
        Assert.Contains(events, e => e.GetProperty("actorRole").GetString() == "Owner");
        var clinicId = await _f.WithDbAsync(db => db.ClinicMembers.Where(m => m.UserId == w.Owner.UserId).Select(m => m.ClinicId).FirstAsync());
        Assert.All(await _f.WithDbAsync(db => db.AuditEvents.Where(a => a.PatientId == pid).ToListAsync()), a => Assert.Equal(clinicId, a.ClinicId));

        // the other doctor is not this patient's treating doctor: same 404 as a missing patient
        Assert.Equal(HttpStatusCode.NotFound, (await _f.ClientFor(w.Doctor.Token).GetAsync($"/api/patients/{pid}/audit-log")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(w.Nurse.Token).GetAsync($"/api/patients/{pid}/audit-log")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(w.Receptionist.Token).GetAsync($"/api/patients/{pid}/audit-log")).StatusCode);

        // a Doctor reads the log of their own patient
        var docPid = await _f.CreatePatientAsync(w.Doctor.Token, $"dp-{Guid.NewGuid():N}@x.com");
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(w.Doctor.Token).GetAsync($"/api/patients/{docPid}/audit-log")).StatusCode);
        // and the owner can read it too
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(w.Owner.Token).GetAsync($"/api/patients/{docPid}/audit-log")).StatusCode);
    }

    // ── Reports ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reports_are_clinic_wide_for_the_owner_and_own_scoped_for_a_doctor()
    {
        var f = _f;
        var w = await f.BuildAsync(); // own clinic so totals are exact
        var ownerPatient = w.PatientId;
        var docPatient = await f.CreatePatientAsync(w.Doctor.Token, $"rp-{Guid.NewGuid():N}@x.com");

        async Task PaidAsync(AuthResult who, int pid, decimal amount)
        {
            var c = f.ClientFor(who.Token);
            var res = await c.PostAsJsonAsync("/api/invoices", new { patientId = pid, serviceDescription = "R", amount });
            res.EnsureSuccessStatusCode();
            var id = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
            (await c.PatchAsync($"/api/invoices/{id}/mark-paid", null)).EnsureSuccessStatusCode();
        }
        await PaidAsync(w.Owner, ownerPatient, 100);
        await PaidAsync(w.Doctor, docPatient, 30);

        var ownerReport = await f.ClientFor(w.Owner.Token).GetFromJsonAsync<JsonElement>("/api/reports/revenue");
        Assert.Equal(130, ownerReport.GetProperty("totalRevenue").GetDecimal());
        var doctorReport = await f.ClientFor(w.Doctor.Token).GetFromJsonAsync<JsonElement>("/api/reports/revenue");
        Assert.Equal(30, doctorReport.GetProperty("totalRevenue").GetDecimal());

        Assert.Equal(HttpStatusCode.Forbidden, (await f.ClientFor(w.Nurse.Token).GetAsync("/api/reports/revenue")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.ClientFor(w.Receptionist.Token).GetAsync("/api/reports/visits")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.ClientFor(w.Receptionist.Token).GetAsync("/api/reports/ar-aging/export")).StatusCode);
    }

    // ── Staff-linked rules ────────────────────────────────────────────────────

    [Fact]
    public void Every_staff_action_declares_a_permission()
    {
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        var missing = new List<string>();
        foreach (var controller in controllers)
        {
            // Sign-in and registration endpoints are public by design
            var anonymousOrPatient = controller == typeof(MedFlow.Api.Controllers.AuthController) || controller.GetCustomAttributes(true).Any(a =>
                a is Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute
                || (a is Microsoft.AspNetCore.Authorization.AuthorizeAttribute au && au.Roles == Roles.Patient));
            foreach (var action in controller.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                         .Where(m => m.GetCustomAttributes(true).Any(a => a is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute)))
            {
                var attrs = controller.GetCustomAttributes(true).Concat(action.GetCustomAttributes(true)).ToList();
                var open = anonymousOrPatient || attrs.Any(a => a is Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute);
                var guarded = attrs.Any(a => a is MedFlow.Api.Authorization.HasPermissionAttribute);
                var legacyDoctorRole = attrs.Any(a => a is Microsoft.AspNetCore.Authorization.AuthorizeAttribute au && au.Roles == Roles.Doctor);
                if (legacyDoctorRole || (!open && !guarded)) missing.Add($"{controller.Name}.{action.Name}");
            }
        }
        Assert.Empty(missing);
    }
}
