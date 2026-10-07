using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedFlow.Core;

namespace MedFlow.Api.Tests;

/// <summary>
/// Regression tests for two suspected authorization gaps: deleting a medical note by id and reading an
/// appointment by id. Both must be clinic-scoped (404 outside the clinic) and permission-guarded (403 for a role
/// without the permission), and a refused request must leave the record untouched.
/// </summary>
public class NoteAndAppointmentAccessTests : IClassFixture<ClinicFixture>
{
    private readonly ClinicFixture _f;
    public NoteAndAppointmentAccessTests(ClinicFixture f) => _f = f;

    private async Task<int> NewNoteAsync(ClinicWorld w)
    {
        var res = await _f.ClientFor(w.Owner.Token).PostAsJsonAsync("/api/medicalnotes",
            new { patientId = w.PatientId, content = "Note " + Guid.NewGuid(), visitType = "Checkup" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task<int> NewAppointmentAsync(ClinicWorld w)
    {
        var res = await _f.ClientFor(w.Owner.Token).PostAsJsonAsync("/api/appointments",
            new { patientId = w.PatientId, scheduledAt = DateTime.UtcNow.AddDays(3), durationMinutes = 30, type = "FollowUp" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    // A fresh patient per test: a patient can only be onboarded to the portal once
    private async Task<AuthResult> NewPortalPatientAsync(ClinicWorld w)
    {
        var email = $"portal-{Guid.NewGuid():N}@x.com";
        var pid = await _f.CreatePatientAsync(w.Owner.Token, email);
        return await _f.OnboardPatientAsync(w.Owner.Token, pid, email);
    }

    private async Task<bool> NoteExistsAsync(ClinicWorld w, int noteId)
    {
        var notes = await _f.ClientFor(w.Owner.Token).GetFromJsonAsync<JsonElement>($"/api/medicalnotes/patient/{w.PatientId}");
        return notes.EnumerateArray().Any(n => n.GetProperty("id").GetInt32() == noteId);
    }

    [Fact]
    public async Task Another_clinics_staff_cannot_delete_a_note_and_it_survives()
    {
        var w = await _f.WorldAsync();
        var noteId = await NewNoteAsync(w);
        var outsider = await _f.RegisterDoctorAsync($"out-{Guid.NewGuid():N}@x.com");

        var res = await _f.ClientFor(outsider.Token).DeleteAsync($"/api/medicalnotes/{noteId}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.True(await NoteExistsAsync(w, noteId));
    }

    [Theory]
    [InlineData(ClinicRole.Nurse)]
    [InlineData(ClinicRole.Receptionist)]
    public async Task Roles_without_notes_manage_cannot_delete_a_note(ClinicRole role)
    {
        var w = await _f.WorldAsync();
        var noteId = await NewNoteAsync(w);
        var actor = _f.ClientFor(w.For(role).Token);

        // A nurse may author a note but not delete it, not even their own (NotesManage is Owner and Doctor only)
        if (role == ClinicRole.Nurse)
        {
            var own = await actor.PostAsJsonAsync("/api/medicalnotes",
                new { patientId = w.PatientId, content = "Nurse note", visitType = "Checkup" });
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
            var ownId = (await own.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
            Assert.Equal(HttpStatusCode.Forbidden, (await actor.DeleteAsync($"/api/medicalnotes/{ownId}")).StatusCode);
            Assert.True(await NoteExistsAsync(w, ownId));
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await actor.DeleteAsync($"/api/medicalnotes/{noteId}")).StatusCode);
        Assert.True(await NoteExistsAsync(w, noteId));
    }

    [Fact]
    public async Task Patient_and_anonymous_callers_cannot_delete_a_note()
    {
        var w = await _f.WorldAsync();
        var noteId = await NewNoteAsync(w);
        var portal = await NewPortalPatientAsync(w);

        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(portal.Token).DeleteAsync($"/api/medicalnotes/{noteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.ClientFor().DeleteAsync($"/api/medicalnotes/{noteId}")).StatusCode);
        Assert.True(await NoteExistsAsync(w, noteId));
    }

    [Fact]
    public async Task Owner_and_doctor_with_notes_manage_can_delete_a_clinic_note()
    {
        var w = await _f.WorldAsync();
        foreach (var role in new[] { ClinicRole.Owner, ClinicRole.Doctor })
        {
            var noteId = await NewNoteAsync(w);
            Assert.Equal(HttpStatusCode.NoContent, (await _f.ClientFor(w.For(role).Token).DeleteAsync($"/api/medicalnotes/{noteId}")).StatusCode);
            Assert.False(await NoteExistsAsync(w, noteId));
        }
    }

    [Theory]
    [InlineData(ClinicRole.Nurse)]
    [InlineData(ClinicRole.Receptionist)]
    public async Task Roles_without_notes_manage_cannot_change_note_sharing(ClinicRole role)
    {
        var w = await _f.WorldAsync();
        var noteId = await NewNoteAsync(w);
        var res = await _f.ClientFor(w.For(role).Token).PutAsJsonAsync($"/api/medicalnotes/{noteId}/sharing", new { shared = true });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Another_clinics_staff_get_404_for_an_appointment_identical_to_a_missing_one()
    {
        var w = await _f.WorldAsync();
        var apptId = await NewAppointmentAsync(w);
        var outsider = await _f.RegisterDoctorAsync($"out-{Guid.NewGuid():N}@x.com");
        var c = _f.ClientFor(outsider.Token);

        var real = await c.GetAsync($"/api/appointments/{apptId}");
        var gone = await c.GetAsync("/api/appointments/99999999");

        Assert.Equal(HttpStatusCode.NotFound, real.StatusCode);
        Assert.Equal(gone.StatusCode, real.StatusCode);
        var trace = new System.Text.RegularExpressions.Regex("\"traceId\":\"[^\"]*\"");
        Assert.Equal(trace.Replace(await gone.Content.ReadAsStringAsync(), ""), trace.Replace(await real.Content.ReadAsStringAsync(), ""));
    }

    [Fact]
    public async Task Patient_and_anonymous_callers_cannot_read_a_staff_appointment()
    {
        var w = await _f.WorldAsync();
        var apptId = await NewAppointmentAsync(w);
        var portal = await NewPortalPatientAsync(w);

        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(portal.Token).GetAsync($"/api/appointments/{apptId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.ClientFor().GetAsync($"/api/appointments/{apptId}")).StatusCode);
    }

    [Theory]
    [InlineData(ClinicRole.Owner)]
    [InlineData(ClinicRole.Doctor)]
    [InlineData(ClinicRole.Nurse)]
    [InlineData(ClinicRole.Receptionist)]
    public async Task Every_clinic_role_may_read_a_clinic_appointment_per_the_matrix(ClinicRole role)
    {
        // AppointmentsRead is granted to all four roles and appointments are clinic-wide, not per-doctor
        Assert.True(PermissionMatrix.Has(role, Permission.AppointmentsRead));
        var w = await _f.WorldAsync();
        var apptId = await NewAppointmentAsync(w);

        var res = await _f.ClientFor(w.For(role).Token).GetAsync($"/api/appointments/{apptId}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
