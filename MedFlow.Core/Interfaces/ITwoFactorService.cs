using MedFlow.Core.DTOs;

namespace MedFlow.Core.Interfaces;

public enum SecondFactorResult { Ok, Invalid, LockedOut }

/// <summary>TOTP two-factor authentication for one user. Every failure that counts toward lockout is recorded here.</summary>
public interface ITwoFactorService
{
    Task<TwoFactorStatusDto> GetStatusAsync(string userId);

    /// <summary>Replaces any pending secret and returns it. Null when 2FA is already on (the active secret is never revealed).</summary>
    Task<TwoFactorSetupDto?> BeginSetupAsync(string userId);

    /// <summary>Activates 2FA when the code is valid; returns the 10 recovery codes (shown once). Null when the code is wrong or 2FA is not pending.</summary>
    Task<IReadOnlyList<string>?> EnableAsync(string userId, string code);

    /// <summary>Checks a TOTP code at login. Replayed steps are rejected; success clears the failure counter.</summary>
    Task<SecondFactorResult> VerifyCodeAsync(string userId, string code);

    /// <summary>Checks and spends a recovery code at login.</summary>
    Task<SecondFactorResult> VerifyRecoveryCodeAsync(string userId, string recoveryCode);

    /// <summary>Disables 2FA after password (when the account has one) and a TOTP or recovery code.</summary>
    Task<SecondFactorResult> DisableAsync(string userId, string? password, string code);

    /// <summary>Replaces the recovery codes after password (when set) and a TOTP or recovery code. Codes are null unless Ok.</summary>
    Task<(SecondFactorResult Result, IReadOnlyList<string>? Codes)> RegenerateRecoveryCodesAsync(string userId, string? password, string code);

    /// <summary>True when the user has 2FA on.</summary>
    Task<bool> IsEnabledAsync(string userId);

    /// <summary>
    /// Password step for a 2FA account. Counts a wrong password toward lockout but never resets the counter on success
    /// (only a completed second factor does).
    /// </summary>
    Task<SecondFactorResult> CheckPasswordForChallengeAsync(string userId, string password);
}
