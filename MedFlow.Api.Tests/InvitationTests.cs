using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

public class InvitationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public InvitationTests(TestApiFactory f) => _f = f;

    private async Task AssertGenericInvalid(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("This invitation is invalid or has expired.",
            (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Invite_is_rejected_for_missing_email_other_doctors_and_inactive_patients()
    {
        var d1 = await _f.RegisterDoctorAsync("d1-inv@x.com");
        var d2 = await _f.RegisterDoctorAsync("d2-inv@x.com");
        var noEmail = await _f.CreatePatientAsync(d1.Token, "");
        var pid = await _f.CreatePatientAsync(d1.Token, "p-inv@x.com");

        Assert.Equal(HttpStatusCode.BadRequest, (await _f.ClientFor(d1.Token).PostAsync($"/api/patients/{noEmail}/portal-invitation", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _f.ClientFor(d2.Token).PostAsync($"/api/patients/{pid}/portal-invitation", null)).StatusCode);

        await _f.WithDbAsync(async db =>
        {
            (await db.Patients.FirstAsync(x => x.Id == pid)).Status = MedFlow.Core.Enums.PatientStatus.Inactive;
            return await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await _f.ClientFor(d1.Token).PostAsync($"/api/patients/{pid}/portal-invitation", null)).StatusCode);
    }

    [Fact]
    public async Task Accepting_creates_a_linked_Patient_user_and_the_token_is_single_use()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-once@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-once@x.com");
        var token = await _f.InviteAsync(doctor.Token, pid);

        var ok = await _f.AcceptAsync(token, "p-once@x.com");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("Patient", (await TestApiFactory.ReadAuth(ok)).Role);
        var linked = await _f.WithDbAsync(db => db.Patients.Where(p => p.Id == pid).Select(p => p.PortalUserId).FirstAsync());
        Assert.NotNull(linked);

        await AssertGenericInvalid(await _f.AcceptAsync(token, "p-once@x.com"));
    }

    [Fact]
    public async Task Unknown_wrong_email_expired_and_superseded_invitations_all_fail_identically()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-bad@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-bad@x.com");

        await AssertGenericInvalid(await _f.AcceptAsync("not-a-token", "p-bad@x.com"));

        var first = await _f.InviteAsync(doctor.Token, pid);
        await AssertGenericInvalid(await _f.AcceptAsync(first, "someone-else@x.com")); // wrong email

        var second = await _f.InviteAsync(doctor.Token, pid); // supersedes `first`
        await AssertGenericInvalid(await _f.AcceptAsync(first, "p-bad@x.com"));

        await _f.WithDbAsync(async db =>
        {
            foreach (var i in db.PortalInvitations.Where(i => i.PatientId == pid && i.UsedAt == null))
                i.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            return await db.SaveChangesAsync();
        });
        await AssertGenericInvalid(await _f.AcceptAsync(second, "p-bad@x.com")); // expired
    }

    [Fact]
    public async Task Changing_the_patients_email_invalidates_a_pending_invitation()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-chg@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "old-chg@x.com");
        var token = await _f.InviteAsync(doctor.Token, pid);
        await _f.WithDbAsync(async db =>
        {
            (await db.Patients.FirstAsync(x => x.Id == pid)).Email = "new-chg@x.com";
            return await db.SaveChangesAsync();
        });
        await AssertGenericInvalid(await _f.AcceptAsync(token, "old-chg@x.com"));
        await AssertGenericInvalid(await _f.AcceptAsync(token, "new-chg@x.com"));
    }

    [Fact]
    public async Task Weak_or_mismatched_passwords_are_rejected_without_consuming_the_invitation()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-pw@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-pw@x.com");
        var token = await _f.InviteAsync(doctor.Token, pid);

        var mismatch = await _f.CreateClient().PostAsJsonAsync("/api/auth/accept-invitation",
            new { token, email = "p-pw@x.com", password = TestApiFactory.Password, confirmPassword = "different" });
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _f.AcceptAsync(token, "p-pw@x.com", "weak")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await _f.AcceptAsync(token, "p-pw@x.com")).StatusCode); // still usable
    }

    [Fact]
    public async Task Invitation_cannot_take_over_an_existing_doctor_account_with_the_same_email()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-own@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "colleague@x.com");
        await _f.RegisterDoctorAsync("colleague@x.com"); // a doctor already owns this email
        var token = await _f.InviteAsync(doctor.Token, pid);
        await AssertGenericInvalid(await _f.AcceptAsync(token, "colleague@x.com"));
    }

    [Fact]
    public async Task Revoke_then_reinvite_relinks_the_existing_patient_account()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-re@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-re@x.com");
        var first = await _f.OnboardPatientAsync(doctor.Token, pid, "p-re@x.com");

        await _f.ClientFor(doctor.Token).DeleteAsync($"/api/patients/{pid}/portal-access");
        var again = await _f.InviteAsync(doctor.Token, pid);
        var res = await _f.AcceptAsync(again, "p-re@x.com", "NewPassw0rd1");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var second = await TestApiFactory.ReadAuth(res);
        Assert.Equal(first.UserId, second.UserId);
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(second.Token).GetAsync("/api/portal/me")).StatusCode);
    }

    [Fact]
    public async Task Already_linked_patients_cannot_be_invited_again()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-dup@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-dup@x.com");
        await _f.OnboardPatientAsync(doctor.Token, pid, "p-dup@x.com");
        Assert.Equal(HttpStatusCode.BadRequest, (await _f.ClientFor(doctor.Token).PostAsync($"/api/patients/{pid}/portal-invitation", null)).StatusCode);
    }
}
