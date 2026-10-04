using MedFlow.Api.Extensions;
using MedFlow.Api.Localization;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MedFlow.Api.Controllers;

/// <summary>
/// The signed-in staff member's own two-factor settings. The user always comes from the token. Patient (portal) accounts
/// cannot enrol. Responses that carry secrets or recovery codes are never cached.
/// </summary>
[ApiController]
[Route("api/account/2fa")]
[Authorize]
public class TwoFactorController : ControllerBase
{
    private readonly ITwoFactorService _twoFactor;
    public TwoFactorController(ITwoFactorService twoFactor) => _twoFactor = twoFactor;

    private IActionResult? RefusePatient() =>
        User.IsInRole(Roles.Patient) ? StatusCode(StatusCodes.Status403Forbidden, new { error = this.T("Error.AccessDenied") }) : null;

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }

    private IActionResult Failure(SecondFactorResult result) => result == SecondFactorResult.LockedOut
        ? Unauthorized(new { error = this.T("Auth.AccountLocked") })
        : BadRequest(new { error = this.T("TwoFactor.InvalidConfirmation") });

    [HttpGet]
    public async Task<IActionResult> Status() =>
        RefusePatient() ?? Ok(await _twoFactor.GetStatusAsync(User.GetUserId()));

    [HttpPost("setup")]
    public async Task<IActionResult> Setup()
    {
        if (RefusePatient() is { } refused) return refused;
        NoStore();
        var setup = await _twoFactor.BeginSetupAsync(User.GetUserId());
        return setup == null ? Conflict(new { error = this.T("TwoFactor.AlreadyEnabled") }) : Ok(setup);
    }

    [HttpPost("enable")]
    [EnableRateLimiting("two-factor")]
    public async Task<IActionResult> Enable([FromBody] TwoFactorEnableRequest req)
    {
        if (RefusePatient() is { } refused) return refused;
        NoStore();
        var userId = User.GetUserId();
        if (await _twoFactor.IsEnabledAsync(userId)) return Conflict(new { error = this.T("TwoFactor.AlreadyEnabled") });
        var codes = await _twoFactor.EnableAsync(userId, req.Code ?? string.Empty);
        return codes == null
            ? BadRequest(new { error = this.T("TwoFactor.InvalidCode") })
            : Ok(new RecoveryCodesDto(codes));
    }

    [HttpPost("recovery-codes")]
    [EnableRateLimiting("two-factor")]
    public async Task<IActionResult> RegenerateRecoveryCodes([FromBody] TwoFactorConfirmRequest req)
    {
        if (RefusePatient() is { } refused) return refused;
        NoStore();
        var (result, codes) = await _twoFactor.RegenerateRecoveryCodesAsync(User.GetUserId(), req.Password, req.Code ?? string.Empty);
        return result == SecondFactorResult.Ok ? Ok(new RecoveryCodesDto(codes!)) : Failure(result);
    }

    [HttpPost("disable")]
    [EnableRateLimiting("two-factor")]
    public async Task<IActionResult> Disable([FromBody] TwoFactorConfirmRequest req)
    {
        if (RefusePatient() is { } refused) return refused;
        var result = await _twoFactor.DisableAsync(User.GetUserId(), req.Password, req.Code ?? string.Empty);
        return result == SecondFactorResult.Ok ? NoContent() : Failure(result);
    }
}
