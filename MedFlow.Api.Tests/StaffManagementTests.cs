using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MedFlow.Core;
using MedFlow.Core.Entities;
using MedFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedFlow.Api.Tests;

/// <summary>Staff invitation flow, membership changes, last-owner protection and fail-closed behaviour.</summary>
public class StaffManagementTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public StaffManagementTests(TestApiFactory f) => _f = f;

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private async Task<AuthResult> NewOwnerAsync() => await _f.RegisterDoctorAsync($"own-{Tag()}@x.com");

    private async Task<JsonElement> StaffAsync(string ownerToken) =>
        await _f.ClientFor(ownerToken).GetFromJsonAsync<JsonElement>("/api/staff?pageSize=100");

    private async Task<int> MemberIdAsync(string ownerToken, string userId) =>
        (await StaffAsync(ownerToken)).GetProperty("items").EnumerateArray()
            .Single(m => m.GetProperty("userId").GetString() == userId).GetProperty("id").GetInt32();

    private static string Sha256(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    // ── Invitation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invitation_email_carries_a_link_and_the_token_is_stored_only_as_a_hash()
    {
        var owner = await NewOwnerAsync();
        var email = $"n-{Tag()}@x.com";
        var token = await _f.InviteStaffAsync(owner.Token, email, "Nurse");

        var mail = _f.Email.Sent.Last(m => m.To == email);
        Assert.Contains("/accept-staff-invite?token=", mail.Body);
        Assert.Contains("Nurse", mail.Body);

        var row = await _f.WithDbAsync(db => db.StaffInvitations.AsNoTracking().SingleAsync(i => i.Email == email));
        Assert.Equal(Sha256(token), row.TokenHash);
        Assert.NotEqual(token, row.TokenHash);
        Assert.Equal(ClinicRole.Nurse, row.Role);
        Assert.Null(row.UsedAt);
        Assert.InRange((row.ExpiresAt - DateTime.UtcNow).TotalDays, 6.9, 7.1);
        // nothing in the database contains the raw token
        Assert.Empty(await _f.WithDbAsync(db => db.StaffInvitations.Where(i => i.TokenHash == token || i.Email == token).ToListAsync()));
    }

    [Fact]
    public async Task Accepting_creates_the_account_joins_the_clinic_with_the_role_and_signs_in()
    {
        var owner = await NewOwnerAsync();
        var email = $"n-{Tag()}@x.com";
        var token = await _f.InviteStaffAsync(owner.Token, email, "Nurse");

        var res = await _f.AcceptStaffAsync(token, email, "Nell", "Nightingale");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var nurse = await TestApiFactory.ReadAuth(res);

        // can work as a nurse right away and can log in again with the password
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(nurse.Token).GetAsync("/api/patients")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(nurse.Token).GetAsync("/api/invoices")).StatusCode);
        var login = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = TestApiFactory.Password });
        var user = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user");
        Assert.Equal("Nurse", user.GetProperty("role").GetString());
        Assert.Equal("Nell", user.GetProperty("firstName").GetString());
        Assert.StartsWith("Clinic of", user.GetProperty("clinicName").GetString());

        var member = await _f.WithDbAsync(db => db.ClinicMembers.AsNoTracking().SingleAsync(m => m.UserId == nurse.UserId));
        Assert.Equal(ClinicRole.Nurse, member.Role);
        Assert.True(member.IsActive);
        var ownerClinic = await _f.WithDbAsync(db => db.ClinicMembers.Where(m => m.UserId == owner.UserId).Select(m => m.ClinicId).SingleAsync());
        Assert.Equal(ownerClinic, member.ClinicId);
        // a nurse has no doctor profile; an invitation is consumed
        Assert.False(await _f.WithDbAsync(db => db.Doctors.AnyAsync(d => d.UserId == nurse.UserId)));
        Assert.NotNull((await _f.WithDbAsync(db => db.StaffInvitations.SingleAsync(i => i.Email == email))).UsedAt);
    }

    [Fact]
    public async Task Doctor_invitee_gets_a_doctor_profile_and_is_listed_as_a_clinic_doctor()
    {
        var owner = await NewOwnerAsync();
        var email = $"d-{Tag()}@x.com";
        var doc = await _f.JoinStaffAsync(owner.Token, email, "Doctor", "Dora", "Docs");
        Assert.True(await _f.WithDbAsync(db => db.Doctors.AnyAsync(d => d.UserId == doc.UserId)));

        var doctors = await _f.ClientFor(owner.Token).GetFromJsonAsync<JsonElement>("/api/clinic/doctors");
        var ids = doctors.EnumerateArray().Select(d => d.GetProperty("userId").GetString()).ToList();
        Assert.Contains(doc.UserId, ids);
        Assert.Contains(owner.UserId, ids);
        Assert.Equal(2, ids.Count);

        // a nurse is not a clinic doctor
        await _f.JoinStaffAsync(owner.Token, $"n-{Tag()}@x.com", "Nurse");
        var again = await _f.ClientFor(owner.Token).GetFromJsonAsync<JsonElement>("/api/clinic/doctors");
        Assert.Equal(2, again.GetArrayLength());
    }

    [Fact]
    public async Task Every_invalid_token_case_returns_the_same_generic_error()
    {
        var owner = await NewOwnerAsync();
        var bodies = new List<string>();
        async Task ExpectInvalid(string token, string email)
        {
            var res = await _f.AcceptStaffAsync(token, email);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            bodies.Add(await res.Content.ReadAsStringAsync());
        }

        // unknown and tampered
        var email = $"t-{Tag()}@x.com";
        var token = await _f.InviteStaffAsync(owner.Token, email, "Nurse");
        await ExpectInvalid("not-a-token", email);
        await ExpectInvalid(token + "x", email);
        // wrong email for a valid token
        await ExpectInvalid(token, $"other-{Tag()}@x.com");
        // expired
        await _f.WithDbAsync(async db =>
        {
            var inv = await db.StaffInvitations.SingleAsync(i => i.Email == email);
            inv.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
            return 0;
        });
        await ExpectInvalid(token, email);

        // superseded: a newer invitation replaces the older one
        var email2 = $"s-{Tag()}@x.com";
        var first = await _f.InviteStaffAsync(owner.Token, email2, "Nurse");
        var second = await _f.InviteStaffAsync(owner.Token, email2, "Receptionist");
        await ExpectInvalid(first, email2);

        // revoked
        var email3 = $"r-{Tag()}@x.com";
        var third = await _f.InviteStaffAsync(owner.Token, email3, "Nurse");
        var pending = await _f.ClientFor(owner.Token).GetFromJsonAsync<JsonElement>("/api/staff/invitations");
        var id = pending.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("email").GetString() == email3).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await _f.ClientFor(owner.Token).DeleteAsync($"/api/staff/invitations/{id}")).StatusCode);
        await ExpectInvalid(third, email3);

        // used
        var okRes = await _f.AcceptStaffAsync(second, email2);
        Assert.Equal(HttpStatusCode.OK, okRes.StatusCode);
        Assert.Equal("Receptionist", (await TestApiFactory.ReadAuth(okRes)).Role);
        await ExpectInvalid(second, email2);

        Assert.True(bodies.Distinct().Count() == 1, "all failure bodies must be identical: " + string.Join(" | ", bodies.Distinct()));
        Assert.Contains("error", bodies[0]);
    }

    [Fact]
    public async Task Inviting_an_address_that_already_has_an_account_looks_exactly_like_success_and_sends_nothing()
    {
        var owner = await NewOwnerAsync();
        var other = await _f.RegisterDoctorAsync($"existing-{Tag()}@x.com");
        var patientEmail = $"pat-{Tag()}@x.com";
        var pid = await _f.CreatePatientAsync(owner.Token, patientEmail);
        await _f.OnboardPatientAsync(owner.Token, pid, patientEmail);

        var c = _f.ClientFor(owner.Token);
        var fresh = await c.PostAsJsonAsync("/api/staff/invitations", new { email = $"fresh-{Tag()}@x.com", role = "Nurse" });
        var before = _f.Email.Sent.Count;
        var doctorEmail = (await _f.WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == other.UserId))).Email!;
        var existingDoctor = await c.PostAsJsonAsync("/api/staff/invitations", new { email = doctorEmail, role = "Nurse" });
        var existingPatient = await c.PostAsJsonAsync("/api/staff/invitations", new { email = patientEmail, role = "Doctor" });

        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        Assert.Equal(HttpStatusCode.OK, existingDoctor.StatusCode);
        Assert.Equal(HttpStatusCode.OK, existingPatient.StatusCode);
        var freshMsg = (await fresh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString();
        Assert.Equal(freshMsg, (await existingDoctor.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
        Assert.Equal(freshMsg, (await existingPatient.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
        Assert.Equal(before, _f.Email.Sent.Count); // no email to the existing accounts
        Assert.Empty(await _f.WithDbAsync(db => db.StaffInvitations.Where(i => i.Email == doctorEmail || i.Email == patientEmail).ToListAsync()));
    }

    [Fact]
    public async Task Invitation_validation_owner_role_bad_email_and_missing_role()
    {
        var owner = await NewOwnerAsync();
        var c = _f.ClientFor(owner.Token);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/staff/invitations", new { email = $"o-{Tag()}@x.com", role = "Owner" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/staff/invitations", new { email = "not-an-email", role = "Nurse" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/staff/invitations", new { email = $"m-{Tag()}@x.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/staff/invitations", new { email = $"m-{Tag()}@x.com", role = "Wizard" })).StatusCode);
    }

    [Fact]
    public async Task Accept_validates_passwords_and_names_and_cannot_take_over_an_existing_account()
    {
        var owner = await NewOwnerAsync();
        var email = $"v-{Tag()}@x.com";
        var token = await _f.InviteStaffAsync(owner.Token, email, "Nurse");
        var c = _f.CreateClient();

        var mismatch = await c.PostAsJsonAsync("/api/auth/accept-staff-invitation",
            new { token, email, password = TestApiFactory.Password, confirmPassword = "Different1", firstName = "A", lastName = "B" });
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        var weak = await c.PostAsJsonAsync("/api/auth/accept-staff-invitation",
            new { token, email, password = "short", confirmPassword = "short", firstName = "A", lastName = "B" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        var noName = await c.PostAsJsonAsync("/api/auth/accept-staff-invitation",
            new { token, email, password = TestApiFactory.Password, confirmPassword = TestApiFactory.Password, firstName = " ", lastName = "B" });
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        // none of those consumed the invitation
        Assert.Equal(HttpStatusCode.OK, (await _f.AcceptStaffAsync(token, email)).StatusCode);

        // an account registered with the invited address in the meantime is never taken over
        var email2 = $"late-{Tag()}@x.com";
        var token2 = await _f.InviteStaffAsync(owner.Token, email2, "Nurse");
        await _f.RegisterDoctorAsync(email2);
        Assert.Equal(HttpStatusCode.BadRequest, (await _f.AcceptStaffAsync(token2, email2)).StatusCode);
    }

    // ── Management ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Owner_lists_staff_and_pending_invitations_with_paging()
    {
        var owner = await NewOwnerAsync();
        await _f.JoinStaffAsync(owner.Token, $"a-{Tag()}@x.com", "Nurse", "Ann", "A");
        await _f.JoinStaffAsync(owner.Token, $"b-{Tag()}@x.com", "Receptionist", "Bob", "B");
        var pendingEmail = $"p-{Tag()}@x.com";
        await _f.InviteStaffAsync(owner.Token, pendingEmail, "Doctor");

        var staff = await _f.ClientFor(owner.Token).GetFromJsonAsync<JsonElement>("/api/staff?page=1&pageSize=2");
        Assert.Equal(3, staff.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, staff.GetProperty("items").GetArrayLength());
        var all = await StaffAsync(owner.Token);
        var roles = all.GetProperty("items").EnumerateArray().Select(m => m.GetProperty("role").GetString()).ToList();
        Assert.Equal(new[] { "Owner", "Nurse", "Receptionist" }, roles);
        Assert.All(all.GetProperty("items").EnumerateArray(), m => Assert.True(m.GetProperty("isActive").GetBoolean()));

        var inv = await _f.ClientFor(owner.Token).GetFromJsonAsync<JsonElement>("/api/staff/invitations");
        var only = inv.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(pendingEmail, only.GetProperty("email").GetString());
        Assert.Equal("Doctor", only.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Role_change_applies_to_the_very_next_request_with_the_old_token()
    {
        var owner = await NewOwnerAsync();
        var nurse = await _f.JoinStaffAsync(owner.Token, $"n-{Tag()}@x.com", "Nurse");
        var pid = await _f.CreatePatientAsync(owner.Token, $"p-{Tag()}@x.com");
        var c = _f.ClientFor(nurse.Token);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/vitalsigns/patient/{pid}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/invoices")).StatusCode);

        var id = await MemberIdAsync(owner.Token, nurse.UserId);
        var change = await _f.ClientFor(owner.Token).PutAsJsonAsync($"/api/staff/{id}/role", new { role = "Receptionist" });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Equal("Receptionist", (await change.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());

        // same token, new permissions: the token role claim is not trusted
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/vitalsigns/patient/{pid}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/invoices")).StatusCode);
    }

    [Fact]
    public async Task Promoting_a_nurse_to_doctor_creates_the_doctor_profile()
    {
        var owner = await NewOwnerAsync();
        var nurse = await _f.JoinStaffAsync(owner.Token, $"n-{Tag()}@x.com", "Nurse");
        Assert.False(await _f.WithDbAsync(db => db.Doctors.AnyAsync(d => d.UserId == nurse.UserId)));
        var id = await MemberIdAsync(owner.Token, nurse.UserId);
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(owner.Token).PutAsJsonAsync($"/api/staff/{id}/role", new { role = "Doctor" })).StatusCode);
        Assert.True(await _f.WithDbAsync(db => db.Doctors.AnyAsync(d => d.UserId == nurse.UserId)));
        // and can now prescribe
        var pid = await _f.CreatePatientAsync(owner.Token, $"p-{Tag()}@x.com");
        var rx = await _f.ClientFor(nurse.Token).PostAsJsonAsync("/api/prescriptions", new
        {
            patientId = pid, drugName = "X", dosage = "1", frequency = "d",
            issuedDate = DateTime.UtcNow.ToString("yyyy-MM-dd"), expiryDate = DateTime.UtcNow.AddDays(9).ToString("yyyy-MM-dd"), refillsRemaining = 0
        });
        Assert.Equal(HttpStatusCode.Created, rx.StatusCode);
    }

    [Fact]
    public async Task Deactivated_member_is_denied_at_once_and_reactivation_restores_access()
    {
        var owner = await NewOwnerAsync();
        var nurse = await _f.JoinStaffAsync(owner.Token, $"n-{Tag()}@x.com", "Nurse");
        var c = _f.ClientFor(nurse.Token);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/patients")).StatusCode);

        var id = await MemberIdAsync(owner.Token, nurse.UserId);
        var off = await _f.ClientFor(owner.Token).PostAsync($"/api/staff/{id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await off.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/patients")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/clinic")).StatusCode);

        // signing in again yields a token with no role and no clinic
        var login = await _f.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = (await _f.WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == nurse.UserId))).Email, password = TestApiFactory.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var relog = await TestApiFactory.ReadAuth(login);
        Assert.Equal("", relog.Role);
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(relog.Token).GetAsync("/api/patients")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(owner.Token).PostAsync($"/api/staff/{id}/reactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/patients")).StatusCode);
        // their authored data is kept while inactive
        Assert.True(await _f.WithDbAsync(db => db.ClinicMembers.AnyAsync(m => m.UserId == nurse.UserId)));
    }

    // ── Last owner ────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_only_owner_cannot_be_demoted_or_deactivated()
    {
        var owner = await NewOwnerAsync();
        var id = await MemberIdAsync(owner.Token, owner.UserId);
        var c = _f.ClientFor(owner.Token);

        var demote = await c.PutAsJsonAsync($"/api/staff/{id}/role", new { role = "Doctor" });
        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"/api/staff/{id}/deactivate", null)).StatusCode);
        // assigning Owner to the Owner is a harmless no-op
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/staff/{id}/role", new { role = "Owner" })).StatusCode);

        var member = await _f.WithDbAsync(db => db.ClinicMembers.AsNoTracking().SingleAsync(m => m.Id == id));
        Assert.Equal(ClinicRole.Owner, member.Role);
        Assert.True(member.IsActive);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/patients")).StatusCode);
    }

    [Fact]
    public async Task With_a_second_owner_one_can_step_down_but_the_last_one_still_cannot()
    {
        var owner = await NewOwnerAsync();
        var second = await _f.JoinStaffAsync(owner.Token, $"o2-{Tag()}@x.com", "Doctor");
        var secondId = await MemberIdAsync(owner.Token, second.UserId);
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(owner.Token).PutAsJsonAsync($"/api/staff/{secondId}/role", new { role = "Owner" })).StatusCode);

        var firstId = await MemberIdAsync(owner.Token, owner.UserId);
        // the first owner steps down
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(owner.Token).PutAsJsonAsync($"/api/staff/{firstId}/role", new { role = "Nurse" })).StatusCode);
        // now they are no owner: staff management is gone for them
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(owner.Token).GetAsync("/api/staff")).StatusCode);
        // the remaining owner cannot demote or deactivate themselves
        var c = _f.ClientFor(second.Token);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/staff/{secondId}/role", new { role = "Doctor" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"/api/staff/{secondId}/deactivate", null)).StatusCode);
    }

    [Fact]
    public async Task Two_owners_demoting_each_other_at_once_leave_exactly_one_owner()
    {
        var owner = await NewOwnerAsync();
        var second = await _f.JoinStaffAsync(owner.Token, $"o2-{Tag()}@x.com", "Doctor");
        var secondId = await MemberIdAsync(owner.Token, second.UserId);
        var firstId = await MemberIdAsync(owner.Token, owner.UserId);
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(owner.Token).PutAsJsonAsync($"/api/staff/{secondId}/role", new { role = "Owner" })).StatusCode);

        for (var round = 0; round < 5; round++)
        {
            var both = await Task.WhenAll(
                _f.ClientFor(owner.Token).PutAsJsonAsync($"/api/staff/{secondId}/role", new { role = "Nurse" }),
                _f.ClientFor(second.Token).PutAsJsonAsync($"/api/staff/{firstId}/role", new { role = "Nurse" }));
            var codes = both.Select(r => r.StatusCode).OrderBy(c => (int)c).ToList();
            Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, codes);
            var owners = await _f.WithDbAsync(db => db.ClinicMembers.CountAsync(m => (m.Id == firstId || m.Id == secondId) && m.Role == ClinicRole.Owner && m.IsActive));
            Assert.Equal(1, owners);

            // restore both as owners for the next round, using whoever is still owner
            var survivor = await _f.WithDbAsync(db => db.ClinicMembers.Where(m => (m.Id == firstId || m.Id == secondId) && m.Role == ClinicRole.Owner).Select(m => m.UserId).SingleAsync());
            var survivorToken = survivor == owner.UserId ? owner.Token : second.Token;
            var demotedId = survivor == owner.UserId ? secondId : firstId;
            Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(survivorToken).PutAsJsonAsync($"/api/staff/{demotedId}/role", new { role = "Owner" })).StatusCode);
        }
    }

    [Fact]
    public async Task Changing_a_member_of_another_clinic_is_a_plain_404()
    {
        var a = await NewOwnerAsync();
        var b = await NewOwnerAsync();
        var nurse = await _f.JoinStaffAsync(a.Token, $"n-{Tag()}@x.com", "Nurse");
        var id = await MemberIdAsync(a.Token, nurse.UserId);
        var c = _f.ClientFor(b.Token);

        Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync($"/api/staff/{id}/role", new { role = "Owner" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"/api/staff/{id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"/api/staff/{id}/reactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync("/api/staff/987654/role", new { role = "Owner" })).StatusCode);
        // the member list of B never shows A's nurse
        Assert.DoesNotContain((await StaffAsync(b.Token)).GetProperty("items").EnumerateArray(), m => m.GetProperty("userId").GetString() == nurse.UserId);
        // invitations of A are not revocable by B
        await _f.InviteStaffAsync(a.Token, $"inv-{Tag()}@x.com", "Nurse");
        var invId = (await _f.ClientFor(a.Token).GetFromJsonAsync<JsonElement>("/api/staff/invitations")).GetProperty("items")[0].GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync($"/api/staff/invitations/{invId}")).StatusCode);
        Assert.Equal(0, (await _f.ClientFor(b.Token).GetFromJsonAsync<JsonElement>("/api/staff/invitations")).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Owner_renames_the_clinic_and_others_read_it()
    {
        var owner = await NewOwnerAsync();
        var nurse = await _f.JoinStaffAsync(owner.Token, $"n-{Tag()}@x.com", "Nurse");
        var res = await _f.ClientFor(owner.Token).PutAsJsonAsync("/api/clinic", new { name = "  Sunrise Clinic  " });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var seen = await _f.ClientFor(nurse.Token).GetFromJsonAsync<JsonElement>("/api/clinic");
        Assert.Equal("Sunrise Clinic", seen.GetProperty("name").GetString());
        Assert.Equal("Nurse", seen.GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(nurse.Token).PutAsJsonAsync("/api/clinic", new { name = "Hacked" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _f.ClientFor(owner.Token).PutAsJsonAsync("/api/clinic", new { name = new string('x', 201) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _f.ClientFor(owner.Token).PutAsJsonAsync("/api/clinic", new { name = "  " })).StatusCode);
    }

    // ── Fail closed ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_user_with_no_clinic_membership_sees_and_does_nothing()
    {
        // an account that exists (even with the legacy Doctor identity role) but belongs to no clinic
        var email = $"orphan-{Tag()}@x.com";
        using (var scope = _f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(Roles.Doctor)) await roles.CreateAsync(new IdentityRole(Roles.Doctor));
            var user = new ApplicationUser { UserName = email, Email = email, FirstName = "Or", LastName = "Phan", Specialty = "GP", EmailConfirmed = true };
            Assert.True((await users.CreateAsync(user, TestApiFactory.Password)).Succeeded);
            await users.AddToRoleAsync(user, Roles.Doctor);
        }
        var login = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = TestApiFactory.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var orphan = await TestApiFactory.ReadAuth(login);
        Assert.Equal("", orphan.Role);

        var c = _f.ClientFor(orphan.Token);
        foreach (var path in new[] { "/api/patients", "/api/appointments", "/api/dashboard", "/api/clinic", "/api/staff", "/api/prescriptions",
                     "/api/invoices", "/api/reports/revenue", "/api/notetemplates", "/api/availability", "/api/waitlist" })
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/patients", new
        {
            firstName = "x", lastName = "y", dateOfBirth = "1990-01-01", gender = "Male", bloodType = "OPos", email = "z@x.com", phone = "1"
        })).StatusCode);
    }

    [Fact]
    public async Task Registration_and_google_style_provisioning_create_exactly_one_clinic_and_owner_membership()
    {
        var d = await _f.RegisterDoctorAsync($"prov-{Tag()}@x.com");
        var rows = await _f.WithDbAsync(db => db.ClinicMembers.Where(m => m.UserId == d.UserId).ToListAsync());
        var member = Assert.Single(rows);
        Assert.Equal(ClinicRole.Owner, member.Role);
        Assert.True(member.IsActive);
        Assert.Equal("Clinic of Doc Tor", (await _f.WithDbAsync(db => db.Clinics.SingleAsync(c => c.Id == member.ClinicId))).Name);
        // one clinic per user is a unique index (the in-memory provider used here does not enforce it, so check the model)
        var unique = await _f.WithDbAsync(db => Task.FromResult(db.Model.FindEntityType(typeof(ClinicMember))!.GetIndexes()
            .Any(ix => ix.IsUnique && ix.Properties.Count == 1 && ix.Properties[0].Name == nameof(ClinicMember.UserId))));
        Assert.True(unique);
    }
}
