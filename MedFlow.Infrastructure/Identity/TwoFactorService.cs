using System.Security.Cryptography;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Infrastructure.Identity;

/// <summary>
/// TOTP 2FA on top of Identity's authenticator key and TwoFactorEnabled flag. Failures (wrong password, wrong code)
/// feed Identity's central lockout; secrets and codes are never logged.
/// </summary>
public class TwoFactorService : ITwoFactorService
{
    private const string Issuer = "MedFlow";
    private const string AuthenticatorLoginProvider = "[AspNetUserStore]";
    private const string AuthenticatorKeyName = "AuthenticatorKey";
    private const int RecoveryCodeCount = 10;
    private const int RecoveryCodeLength = 10;
    // No 0/O/1/I/L so codes survive being read aloud or copied by hand
    private const string RecoveryAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly UserManager<ApplicationUser> _users;
    private readonly IPasswordHasher<ApplicationUser> _hasher;
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public TwoFactorService(UserManager<ApplicationUser> users, IPasswordHasher<ApplicationUser> hasher,
        AppDbContext db, IAuditService audit)
    {
        _users = users;
        _hasher = hasher;
        _db = db;
        _audit = audit;
    }

    public async Task<TwoFactorStatusDto> GetStatusAsync(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user == null || !user.TwoFactorEnabled) return new TwoFactorStatusDto(false, 0);
        var remaining = await _db.TwoFactorRecoveryCodes.CountAsync(c => c.UserId == userId && c.UsedAt == null);
        return new TwoFactorStatusDto(true, remaining);
    }

    public async Task<bool> IsEnabledAsync(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        return user != null && user.TwoFactorEnabled;
    }

    public async Task<TwoFactorSetupDto?> BeginSetupAsync(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user == null || user.TwoFactorEnabled) return null;

        user.LastTotpStep = null;
        await _users.ResetAuthenticatorKeyAsync(user); // also saves LastTotpStep
        var key = await _users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key)) return null;

        var label = Uri.EscapeDataString(Issuer) + ":" + Uri.EscapeDataString(user.Email ?? user.UserName ?? userId);
        var uri = $"otpauth://totp/{label}?secret={key}&issuer={Uri.EscapeDataString(Issuer)}" +
                  $"&algorithm=SHA1&digits={TotpVerifier.Digits}&period={TotpVerifier.StepSeconds}";
        return new TwoFactorSetupDto(key, uri);
    }

    public async Task<IReadOnlyList<string>?> EnableAsync(string userId, string code)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user == null || user.TwoFactorEnabled) return null;
        if (await _users.IsLockedOutAsync(user)) return null;
        if (string.IsNullOrEmpty(await _users.GetAuthenticatorKeyAsync(user))) return null; // setup was never started

        if (!await TryTotpAsync(user, code))
        {
            await _users.AccessFailedAsync(user);
            return null;
        }
        await _users.SetTwoFactorEnabledAsync(user, true);
        await _users.ResetAccessFailedCountAsync(user);
        var codes = await ReplaceRecoveryCodesAsync(user);
        await _audit.RecordSecurityAsync(userId, SecurityEventKind.TwoFactorEnabled);
        return codes;
    }

    public async Task<SecondFactorResult> CheckPasswordForChallengeAsync(string userId, string password)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user == null) return SecondFactorResult.Invalid;
        if (await _users.IsLockedOutAsync(user)) return SecondFactorResult.LockedOut;
        if (await _users.CheckPasswordAsync(user, password)) return SecondFactorResult.Ok; // deliberately no counter reset
        await _users.AccessFailedAsync(user);
        return SecondFactorResult.Invalid;
    }

    public async Task<SecondFactorResult> VerifyCodeAsync(string userId, string code)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user == null || !user.TwoFactorEnabled) return SecondFactorResult.Invalid;
        if (await _users.IsLockedOutAsync(user)) return SecondFactorResult.LockedOut;
        if (await TryTotpAsync(user, code))
        {
            await _users.ResetAccessFailedCountAsync(user);
            return SecondFactorResult.Ok;
        }
        await _users.AccessFailedAsync(user);
        return SecondFactorResult.Invalid;
    }

    public async Task<SecondFactorResult> VerifyRecoveryCodeAsync(string userId, string recoveryCode)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user == null || !user.TwoFactorEnabled) return SecondFactorResult.Invalid;
        if (await _users.IsLockedOutAsync(user)) return SecondFactorResult.LockedOut;
        if (await TryRecoveryAsync(user, recoveryCode))
        {
            await _users.ResetAccessFailedCountAsync(user);
            return SecondFactorResult.Ok;
        }
        await _users.AccessFailedAsync(user);
        return SecondFactorResult.Invalid;
    }

    public async Task<SecondFactorResult> DisableAsync(string userId, string? password, string code)
    {
        var (result, user) = await ConfirmAsync(userId, password, code);
        if (result != SecondFactorResult.Ok || user == null) return result;

        await _users.SetTwoFactorEnabledAsync(user, false);
        await _users.RemoveAuthenticationTokenAsync(user, AuthenticatorLoginProvider, AuthenticatorKeyName);
        user.LastTotpStep = null;
        await _users.UpdateAsync(user);
        _db.TwoFactorRecoveryCodes.RemoveRange(_db.TwoFactorRecoveryCodes.Where(c => c.UserId == userId));
        await _db.SaveChangesAsync();
        await _audit.RecordSecurityAsync(userId, SecurityEventKind.TwoFactorDisabled);
        return SecondFactorResult.Ok;
    }

    public async Task<(SecondFactorResult Result, IReadOnlyList<string>? Codes)> RegenerateRecoveryCodesAsync(
        string userId, string? password, string code)
    {
        var (result, user) = await ConfirmAsync(userId, password, code);
        if (result != SecondFactorResult.Ok || user == null) return (result, null);

        var codes = await ReplaceRecoveryCodesAsync(user);
        await _audit.RecordSecurityAsync(userId, SecurityEventKind.RecoveryCodesRegenerated);
        return (SecondFactorResult.Ok, codes);
    }

    /// <summary>
    /// Password (when the account has one) first, then a TOTP or recovery code. The code is only checked, and so only
    /// spent, once the password is right. Any failure counts toward lockout and looks the same.
    /// </summary>
    private async Task<(SecondFactorResult, ApplicationUser?)> ConfirmAsync(string userId, string? password, string code)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user == null || !user.TwoFactorEnabled) return (SecondFactorResult.Invalid, null);
        if (await _users.IsLockedOutAsync(user)) return (SecondFactorResult.LockedOut, null);

        var passwordOk = !await _users.HasPasswordAsync(user) ||
                         (!string.IsNullOrEmpty(password) && await _users.CheckPasswordAsync(user, password));
        var codeOk = passwordOk && (await TryTotpAsync(user, code) || await TryRecoveryAsync(user, code));
        if (!codeOk)
        {
            await _users.AccessFailedAsync(user);
            return (SecondFactorResult.Invalid, null);
        }
        await _users.ResetAccessFailedCountAsync(user);
        return (SecondFactorResult.Ok, user);
    }

    private async Task<bool> TryTotpAsync(ApplicationUser user, string? code)
    {
        var key = await _users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key)) return false;
        var step = TotpVerifier.Verify(key, code, DateTimeOffset.UtcNow);
        if (step == null) return false;
        if (user.LastTotpStep is { } last && step.Value <= last) return false; // replay

        user.LastTotpStep = step.Value;
        // The concurrency stamp makes two simultaneous submissions of the same code race: only one update wins
        var saved = await _users.UpdateAsync(user);
        return saved.Succeeded;
    }

    private async Task<bool> TryRecoveryAsync(ApplicationUser user, string? code)
    {
        var normalized = NormalizeRecoveryCode(code);
        var unused = await _db.TwoFactorRecoveryCodes.Where(c => c.UserId == user.Id && c.UsedAt == null).ToListAsync();

        // Every unused code is hashed whichever one matches, so timing does not reveal which (or whether)
        TwoFactorRecoveryCode? match = null;
        foreach (var candidate in unused)
        {
            var verdict = _hasher.VerifyHashedPassword(user, candidate.CodeHash, normalized ?? "-");
            if (normalized != null && verdict != PasswordVerificationResult.Failed && match == null) match = candidate;
        }
        if (match == null) return false;

        match.UsedAt = DateTime.UtcNow;
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return false; } // spent by a concurrent request
        await _audit.RecordSecurityAsync(user.Id, SecurityEventKind.RecoveryCodeUsed);
        return true;
    }

    private async Task<IReadOnlyList<string>> ReplaceRecoveryCodesAsync(ApplicationUser user)
    {
        _db.TwoFactorRecoveryCodes.RemoveRange(_db.TwoFactorRecoveryCodes.Where(c => c.UserId == user.Id));
        var shown = new List<string>(RecoveryCodeCount);
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var raw = new string(Enumerable.Range(0, RecoveryCodeLength)
                .Select(_ => RecoveryAlphabet[RandomNumberGenerator.GetInt32(RecoveryAlphabet.Length)]).ToArray());
            _db.TwoFactorRecoveryCodes.Add(new TwoFactorRecoveryCode
            {
                UserId = user.Id,
                CodeHash = _hasher.HashPassword(user, raw),
                CreatedAt = DateTime.UtcNow
            });
            shown.Add(raw[..5] + "-" + raw[5..]);
        }
        await _db.SaveChangesAsync();
        return shown;
    }

    /// <summary>Upper case, dashes and spaces removed; null when it cannot be a recovery code.</summary>
    private static string? NormalizeRecoveryCode(string? code)
    {
        if (code == null) return null;
        var n = new string(code.Where(c => c is not (' ' or '-')).ToArray()).ToUpperInvariant();
        return n.Length == RecoveryCodeLength && n.All(c => RecoveryAlphabet.Contains(c)) ? n : null;
    }
}
