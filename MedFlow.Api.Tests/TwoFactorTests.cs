using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedFlow.Core.Enums;
using MedFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MedFlow.Api.Tests;

/// <summary>Spec 046: TOTP two-factor authentication.</summary>
public class TwoFactorTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public TwoFactorTests(TestApiFactory f) => _f = f;

    private sealed record Enrolled(AuthResult Auth, string Email, string Key, List<string> RecoveryCodes);

    private static string CodeFor(string key, int stepOffset = 0) =>
        TotpVerifier.Compute(TotpVerifier.DecodeBase32(key)!, TotpVerifier.CurrentStep(DateTimeOffset.UtcNow) + stepOffset);

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>The replay guard remembers the last accepted step; tests that need a fresh valid code clear it.</summary>
    private Task ForgetLastStepAsync(string userId) => _f.WithDbAsync(async db =>
    {
        (await db.Users.FirstAsync(u => u.Id == userId)).LastTotpStep = null;
        return await db.SaveChangesAsync();
    });

    private async Task<Enrolled> EnrollAsync(string email)
    {
        var auth = await _f.RegisterDoctorAsync(email);
        var client = _f.ClientFor(auth.Token);
        var setup = await client.PostAsync("/api/account/2fa/setup", null);
        setup.EnsureSuccessStatusCode();
        var key = (await Json(setup)).GetProperty("sharedKey").GetString()!;
        var enable = await client.PostAsJsonAsync("/api/account/2fa/enable", new { code = CodeFor(key) });
        enable.EnsureSuccessStatusCode();
        var codes = (await Json(enable)).GetProperty("recoveryCodes").EnumerateArray().Select(x => x.GetString()!).ToList();
        return new Enrolled(auth, email, key, codes);
    }

    private async Task<string> ChallengeAsync(string email, string password = TestApiFactory.Password)
    {
        var res = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await Json(res)).GetProperty("challengeToken").GetString()!;
    }

    private Task<HttpResponseMessage> VerifyAsync(string challenge, string code) =>
        _f.CreateClient().PostAsJsonAsync("/api/auth/2fa/verify", new { challengeToken = challenge, code });

    private Task<HttpResponseMessage> RecoverAsync(string challenge, string recoveryCode) =>
        _f.CreateClient().PostAsJsonAsync("/api/auth/2fa/recovery", new { challengeToken = challenge, recoveryCode });

    private static async Task<string> ErrorOf(HttpResponseMessage r) => (await Json(r)).GetProperty("error").GetString()!;

    // ── Verifier ──────────────────────────────────────────────────────────────

    [Fact]
    public void Totp_matches_the_RFC_6238_test_vector_and_rejects_wrong_codes()
    {
        var key = System.Text.Encoding.ASCII.GetBytes("12345678901234567890");
        Assert.Equal("287082", TotpVerifier.Compute(key, 59 / 30)); // RFC 6238 appendix B, T=59, last 6 digits
        const string base32 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ"; // same key
        var now = DateTimeOffset.FromUnixTimeSeconds(59);
        Assert.Equal(1, TotpVerifier.Verify(base32, "287082", now));
        Assert.Null(TotpVerifier.Verify(base32, "287083", now));
        Assert.Null(TotpVerifier.Verify(base32, "28708", now));
        Assert.Null(TotpVerifier.Verify(base32, "abcdef", now));
        Assert.Null(TotpVerifier.Verify(base32, "287082", now.AddSeconds(95))); // three steps away
    }

    // ── Enrolment ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Setup_returns_secret_uncached_and_does_not_enable_until_a_valid_code_is_confirmed()
    {
        var auth = await _f.RegisterDoctorAsync("tf-setup@x.com");
        var client = _f.ClientFor(auth.Token);

        var setup = await client.PostAsync("/api/account/2fa/setup", null);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        Assert.Contains("no-store", setup.Headers.CacheControl!.ToString());
        var body = await Json(setup);
        var key = body.GetProperty("sharedKey").GetString()!;
        Assert.StartsWith("otpauth://totp/MedFlow:", body.GetProperty("otpAuthUri").GetString());
        Assert.Contains("secret=" + key, body.GetProperty("otpAuthUri").GetString());

        var status = await Json(await client.GetAsync("/api/account/2fa"));
        Assert.False(status.GetProperty("enabled").GetBoolean());

        var wrong = await client.PostAsJsonAsync("/api/account/2fa/enable", new { code = "000000" == CodeFor(key) ? "111111" : "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.False((await Json(await client.GetAsync("/api/account/2fa"))).GetProperty("enabled").GetBoolean());

        // Without a started setup nothing can be enabled
        var other = await _f.RegisterDoctorAsync("tf-nosetup@x.com");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _f.ClientFor(other.Token).PostAsJsonAsync("/api/account/2fa/enable", new { code = "123456" })).StatusCode);
    }

    [Fact]
    public async Task Enabling_returns_ten_codes_once_stores_them_hashed_and_records_an_event()
    {
        var e = await EnrollAsync("tf-enable@x.com");
        Assert.Equal(10, e.RecoveryCodes.Count);
        Assert.Equal(10, e.RecoveryCodes.Distinct().Count());
        Assert.All(e.RecoveryCodes, c => Assert.Matches("^[A-Z2-9]{5}-[A-Z2-9]{5}$", c));

        var stored = await _f.WithDbAsync(db => db.TwoFactorRecoveryCodes.Where(c => c.UserId == e.Auth.UserId).ToListAsync());
        Assert.Equal(10, stored.Count);
        foreach (var raw in e.RecoveryCodes)
            Assert.DoesNotContain(stored, s => s.CodeHash.Contains(raw.Replace("-", "")) || s.CodeHash == raw);

        var status = await Json(await _f.ClientFor(e.Auth.Token).GetAsync("/api/account/2fa"));
        Assert.True(status.GetProperty("enabled").GetBoolean());
        Assert.Equal(10, status.GetProperty("recoveryCodesRemaining").GetInt32());

        Assert.Equal(1, await _f.WithDbAsync(db => db.SecurityEvents
            .CountAsync(x => x.UserId == e.Auth.UserId && x.Kind == SecurityEventKind.TwoFactorEnabled)));
    }

    [Fact]
    public async Task Setup_and_enable_are_refused_once_enabled_and_never_reveal_the_secret()
    {
        var e = await EnrollAsync("tf-twice@x.com");
        var client = _f.ClientFor(e.Auth.Token);
        var setup = await client.PostAsync("/api/account/2fa/setup", null);
        Assert.Equal(HttpStatusCode.Conflict, setup.StatusCode);
        Assert.DoesNotContain(e.Key, await setup.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync("/api/account/2fa/enable", new { code = CodeFor(e.Key, 1) })).StatusCode);
    }

    [Fact]
    public async Task Management_endpoints_reject_anonymous_callers_and_patient_tokens()
    {
        var doctor = await _f.RegisterDoctorAsync("tf-doc-pt@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "tf-pt@x.com");
        var patient = await _f.OnboardPatientAsync(doctor.Token, pid, "tf-pt@x.com");

        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync("/api/account/2fa")).StatusCode);
        var c = _f.ClientFor(patient.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/account/2fa")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsync("/api/account/2fa/setup", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/account/2fa/enable", new { code = "123456" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/account/2fa/disable", new { password = TestApiFactory.Password, code = "123456" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/account/2fa/recovery-codes", new { password = TestApiFactory.Password, code = "123456" })).StatusCode);
        // Portal login itself is unchanged
        var login = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "tf-pt@x.com", password = TestApiFactory.Password });
        Assert.True((await Json(login)).TryGetProperty("token", out _));
    }

    // ── Login gate ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Password_login_returns_a_challenge_and_no_session_when_2fa_is_on()
    {
        var e = await EnrollAsync("tf-gate@x.com");
        var res = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = e.Email, password = TestApiFactory.Password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await Json(res);
        Assert.True(body.GetProperty("twoFactorRequired").GetBoolean());
        Assert.False(body.TryGetProperty("token", out _));
        Assert.False(body.TryGetProperty("user", out _));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("challengeToken").GetString()));
    }

    [Fact]
    public async Task A_challenge_cannot_be_used_as_a_session_and_a_session_cannot_be_used_as_a_challenge()
    {
        var e = await EnrollAsync("tf-sep@x.com");
        var challenge = await ChallengeAsync(e.Email);

        var asBearer = _f.ClientFor(challenge);
        Assert.Equal(HttpStatusCode.Unauthorized, (await asBearer.GetAsync("/api/clinic")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await asBearer.GetAsync("/api/account/2fa")).StatusCode);

        await ForgetLastStepAsync(e.Auth.UserId);
        var asChallenge = await VerifyAsync(e.Auth.Token, CodeFor(e.Key));
        Assert.Equal(HttpStatusCode.Unauthorized, asChallenge.StatusCode);
    }

    [Fact]
    public async Task A_valid_code_exchanges_the_challenge_for_a_working_session()
    {
        var e = await EnrollAsync("tf-ok@x.com");
        var challenge = await ChallengeAsync(e.Email);
        await ForgetLastStepAsync(e.Auth.UserId);

        var res = await VerifyAsync(challenge, CodeFor(e.Key));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var auth = await TestApiFactory.ReadAuth(res);
        Assert.Equal("Owner", auth.Role);
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(auth.Token).GetAsync("/api/clinic")).StatusCode);
    }

    [Fact]
    public async Task Every_failure_reads_the_same_and_wrong_password_is_not_distinguishable_from_unprotected_accounts()
    {
        var e = await EnrollAsync("tf-uniform@x.com");
        var plain = await _f.RegisterDoctorAsync("tf-plain@x.com");
        var challenge = await ChallengeAsync(e.Email);
        await ForgetLastStepAsync(e.Auth.UserId);

        var wrongCode = await VerifyAsync(challenge, CodeFor(e.Key, 1) == "000000" ? "111111" : "000000");
        var badChallenge = await VerifyAsync("not-a-token", CodeFor(e.Key));
        var expired = await VerifyAsync(challenge + "x", CodeFor(e.Key));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongCode.StatusCode);
        var msg = await ErrorOf(wrongCode);
        Assert.Equal(HttpStatusCode.Unauthorized, badChallenge.StatusCode);
        Assert.Equal(msg, await ErrorOf(badChallenge));
        Assert.Equal(msg, await ErrorOf(expired));

        var wrongPwProtected = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = e.Email, password = "Wrong1234" });
        var wrongPwPlain = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "tf-plain@x.com", password = "Wrong1234" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPwProtected.StatusCode);
        Assert.Equal(await ErrorOf(wrongPwPlain), await ErrorOf(wrongPwProtected));
        Assert.NotNull(plain);
    }

    [Fact]
    public async Task A_code_cannot_be_replayed()
    {
        var e = await EnrollAsync("tf-replay@x.com");
        await ForgetLastStepAsync(e.Auth.UserId);
        var code = CodeFor(e.Key);

        Assert.Equal(HttpStatusCode.OK, (await VerifyAsync(await ChallengeAsync(e.Email), code)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyAsync(await ChallengeAsync(e.Email), code)).StatusCode);
        // the confirmation code used at enrolment is spent as well
        await ForgetLastStepAsync(e.Auth.UserId);
        Assert.Equal(HttpStatusCode.OK, (await VerifyAsync(await ChallengeAsync(e.Email), CodeFor(e.Key))).StatusCode);
    }

    [Fact]
    public async Task The_enrolment_confirmation_code_cannot_be_reused_at_the_first_login()
    {
        var auth = await _f.RegisterDoctorAsync("tf-confirm-replay@x.com");
        var client = _f.ClientFor(auth.Token);
        var key = (await Json(await client.PostAsync("/api/account/2fa/setup", null))).GetProperty("sharedKey").GetString()!;
        var code = CodeFor(key);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/2fa/enable", new { code })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyAsync(await ChallengeAsync("tf-confirm-replay@x.com"), code)).StatusCode);
    }

    // ── Brute force ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Repeated_wrong_codes_lock_the_account_and_a_correct_code_is_then_refused()
    {
        var e = await EnrollAsync("tf-lock@x.com");
        var challenge = await ChallengeAsync(e.Email);
        await ForgetLastStepAsync(e.Auth.UserId);
        var good = CodeFor(e.Key);
        var bad = good == "123456" ? "654321" : "123456";

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyAsync(challenge, bad)).StatusCode);

        var locked = await VerifyAsync(challenge, good);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal("Cuenta bloqueada. Inténtelo de nuevo más tarde.", await ErrorOf(locked));
        // password login reports the lockout too
        var login = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = e.Email, password = TestApiFactory.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task A_correct_password_does_not_reset_the_failure_counter_of_a_2fa_account()
    {
        var e = await EnrollAsync("tf-noreset@x.com");
        var challenge = await ChallengeAsync(e.Email);
        await ForgetLastStepAsync(e.Auth.UserId);
        var bad = CodeFor(e.Key) == "123456" ? "654321" : "123456";

        for (var i = 0; i < 4; i++) await VerifyAsync(challenge, bad);
        await ChallengeAsync(e.Email); // correct password: would have cleared the counter on a normal account
        await VerifyAsync(challenge, bad); // fifth failure in total

        var login = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = e.Email, password = TestApiFactory.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal("Cuenta bloqueada. Inténtelo de nuevo más tarde.", await ErrorOf(login));
    }

    [Fact]
    public async Task Verify_and_recovery_endpoints_are_rate_limited_per_address()
    {
        var e = await EnrollAsync("tf-rate@x.com");
        var challenge = await ChallengeAsync(e.Email);
        using var limited = _f.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimiting:TwoFactorPermitLimit"] = "2" })));
        var client = limited.CreateClient();

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            codes.Add((await client.PostAsJsonAsync("/api/auth/2fa/verify", new { challengeToken = challenge, code = "000000" })).StatusCode);
        Assert.Equal(new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests }, codes);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.PostAsJsonAsync("/api/auth/2fa/recovery", new { challengeToken = challenge, recoveryCode = "AAAAA-AAAAA" })).StatusCode);
    }

    // ── Recovery codes ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_recovery_code_signs_in_once_tolerates_formatting_and_is_audited()
    {
        var e = await EnrollAsync("tf-rec@x.com");
        var code = e.RecoveryCodes[0];

        var ok = await RecoverAsync(await ChallengeAsync(e.Email), code.ToLowerInvariant().Replace("-", " "));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RecoverAsync(await ChallengeAsync(e.Email), code)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RecoverAsync(await ChallengeAsync(e.Email), e.RecoveryCodes[1])).StatusCode);

        Assert.Equal(2, await _f.WithDbAsync(db => db.SecurityEvents
            .CountAsync(x => x.UserId == e.Auth.UserId && x.Kind == SecurityEventKind.RecoveryCodeUsed)));
        var status = await Json(await _f.ClientFor(e.Auth.Token).GetAsync("/api/account/2fa"));
        Assert.Equal(8, status.GetProperty("recoveryCodesRemaining").GetInt32());
    }

    [Fact]
    public async Task A_recovery_code_of_another_user_or_a_made_up_one_is_refused()
    {
        var a = await EnrollAsync("tf-rec-a@x.com");
        var b = await EnrollAsync("tf-rec-b@x.com");
        Assert.Equal(HttpStatusCode.Unauthorized, (await RecoverAsync(await ChallengeAsync(a.Email), b.RecoveryCodes[0])).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RecoverAsync(await ChallengeAsync(a.Email), "AAAAA-AAAAA")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RecoverAsync(await ChallengeAsync(a.Email), "")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_use_of_one_recovery_code_succeeds_at_most_once()
    {
        var e = await EnrollAsync("tf-race@x.com");
        var c1 = await ChallengeAsync(e.Email);
        var c2 = await ChallengeAsync(e.Email);
        var results = await Task.WhenAll(RecoverAsync(c1, e.RecoveryCodes[0]), RecoverAsync(c2, e.RecoveryCodes[0]));
        Assert.True(results.Count(r => r.StatusCode == HttpStatusCode.OK) <= 1);
    }

    [Fact]
    public async Task Regenerating_needs_password_and_code_and_invalidates_the_old_codes()
    {
        var e = await EnrollAsync("tf-regen@x.com");
        var client = _f.ClientFor(e.Auth.Token);
        await ForgetLastStepAsync(e.Auth.UserId);
        var code = CodeFor(e.Key);

        var wrongPw = await client.PostAsJsonAsync("/api/account/2fa/recovery-codes", new { password = "Wrong1234", code });
        var wrongCode = await client.PostAsJsonAsync("/api/account/2fa/recovery-codes",
            new { password = TestApiFactory.Password, code = code == "123456" ? "654321" : "123456" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongPw.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongCode.StatusCode);
        Assert.Equal(await ErrorOf(wrongPw), await ErrorOf(wrongCode)); // does not say which part was wrong

        await ForgetLastStepAsync(e.Auth.UserId);
        var ok = await client.PostAsJsonAsync("/api/account/2fa/recovery-codes", new { password = TestApiFactory.Password, code = CodeFor(e.Key) });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("no-store", ok.Headers.CacheControl!.ToString());
        var fresh = (await Json(ok)).GetProperty("recoveryCodes").EnumerateArray().Select(x => x.GetString()!).ToList();
        Assert.Equal(10, fresh.Count);
        Assert.Empty(fresh.Intersect(e.RecoveryCodes));

        Assert.Equal(HttpStatusCode.Unauthorized, (await RecoverAsync(await ChallengeAsync(e.Email), e.RecoveryCodes[0])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RecoverAsync(await ChallengeAsync(e.Email), fresh[0])).StatusCode);
        Assert.Equal(1, await _f.WithDbAsync(db => db.SecurityEvents
            .CountAsync(x => x.UserId == e.Auth.UserId && x.Kind == SecurityEventKind.RecoveryCodesRegenerated)));
    }

    // ── Disable ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Disabling_needs_password_and_a_current_code_and_then_login_is_normal_again()
    {
        var e = await EnrollAsync("tf-disable@x.com");
        var client = _f.ClientFor(e.Auth.Token);
        await ForgetLastStepAsync(e.Auth.UserId);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/account/2fa/disable", new { password = "Wrong1234", code = CodeFor(e.Key) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/account/2fa/disable", new { code = CodeFor(e.Key) })).StatusCode); // password missing
        Assert.True((await Json(await client.GetAsync("/api/account/2fa"))).GetProperty("enabled").GetBoolean());

        await ForgetLastStepAsync(e.Auth.UserId);
        var ok = await client.PostAsJsonAsync("/api/account/2fa/disable", new { password = TestApiFactory.Password, code = CodeFor(e.Key) });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        Assert.False((await Json(await client.GetAsync("/api/account/2fa"))).GetProperty("enabled").GetBoolean());
        Assert.Equal(0, await _f.WithDbAsync(db => db.TwoFactorRecoveryCodes.CountAsync(c => c.UserId == e.Auth.UserId)));
        var login = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = e.Email, password = TestApiFactory.Password });
        Assert.True((await Json(login)).TryGetProperty("token", out _));
        Assert.Equal(1, await _f.WithDbAsync(db => db.SecurityEvents
            .CountAsync(x => x.UserId == e.Auth.UserId && x.Kind == SecurityEventKind.TwoFactorDisabled)));

        // a fresh setup starts from a new secret
        var again = await Json(await client.PostAsync("/api/account/2fa/setup", null));
        Assert.NotEqual(e.Key, again.GetProperty("sharedKey").GetString());
    }

    [Fact]
    public async Task A_recovery_code_can_confirm_a_disable_and_is_spent()
    {
        var e = await EnrollAsync("tf-disable-rec@x.com");
        var res = await _f.ClientFor(e.Auth.Token).PostAsJsonAsync("/api/account/2fa/disable",
            new { password = TestApiFactory.Password, code = e.RecoveryCodes[3] });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(1, await _f.WithDbAsync(db => db.SecurityEvents
            .CountAsync(x => x.UserId == e.Auth.UserId && x.Kind == SecurityEventKind.RecoveryCodeUsed)));
    }

    [Fact]
    public async Task Failed_disable_attempts_count_toward_lockout()
    {
        var e = await EnrollAsync("tf-disable-lock@x.com");
        var client = _f.ClientFor(e.Auth.Token);
        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/account/2fa/disable", new { password = TestApiFactory.Password, code = "000000" });
        await ForgetLastStepAsync(e.Auth.UserId);
        var res = await client.PostAsJsonAsync("/api/account/2fa/disable", new { password = TestApiFactory.Password, code = CodeFor(e.Key) });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.True((await Json(await client.GetAsync("/api/account/2fa"))).GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task A_google_only_account_can_disable_with_just_a_code()
    {
        var e = await EnrollAsync("tf-google@x.com");
        await _f.WithDbAsync(async db =>
        {
            (await db.Users.FirstAsync(u => u.Id == e.Auth.UserId)).PasswordHash = null; // as a Google-created account
            return await db.SaveChangesAsync();
        });
        await ForgetLastStepAsync(e.Auth.UserId);
        var res = await _f.ClientFor(e.Auth.Token).PostAsJsonAsync("/api/account/2fa/disable", new { code = CodeFor(e.Key) });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Security_events_hold_no_secrets_or_codes()
    {
        var e = await EnrollAsync("tf-events@x.com");
        await RecoverAsync(await ChallengeAsync(e.Email), e.RecoveryCodes[0]);
        var events = await _f.WithDbAsync(db => db.SecurityEvents.Where(x => x.UserId == e.Auth.UserId).ToListAsync());
        var dump = JsonSerializer.Serialize(events);
        Assert.DoesNotContain(e.Key, dump);
        Assert.All(e.RecoveryCodes, c => Assert.DoesNotContain(c, dump));
    }
}
