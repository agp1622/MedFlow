using MedFlow.Api.Localization;
using MedFlow.Core.DTOs;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MedFlow.Api.Controllers;

/// <summary>Anonymous, token-only endpoints used from the link in a waitlist offer email.</summary>
[ApiController]
[Route("api/waitlist-offer")]
[AllowAnonymous]
[EnableRateLimiting("waitlist-offer")]
public class WaitlistOfferController : ControllerBase
{
    private readonly IWaitlistRepository _waitlist;

    public WaitlistOfferController(IWaitlistRepository waitlist) => _waitlist = waitlist;

    private NotFoundObjectResult Invalid() => NotFound(new { error = this.T("Waitlist.OfferInvalid") });

    [HttpPost("lookup")]
    public async Task<ActionResult<WaitlistOfferDto>> Lookup([FromBody] WaitlistTokenRequest req)
    {
        var dto = await _waitlist.LookupOfferAsync(req.Token);
        return dto == null ? Invalid() : Ok(dto);
    }

    [HttpPost("claim")]
    public async Task<ActionResult<WaitlistClaimDto>> Claim([FromBody] WaitlistTokenRequest req)
    {
        var (outcome, claim) = await _waitlist.ClaimOfferAsync(req.Token);
        return outcome switch
        {
            WaitlistClaimOutcome.Claimed => Ok(claim),
            WaitlistClaimOutcome.SlotUnavailable => Conflict(new { error = this.T("Waitlist.SlotGone") }),
            _ => Invalid()
        };
    }

    [HttpPost("leave")]
    public async Task<IActionResult> Leave([FromBody] WaitlistTokenRequest req) =>
        await _waitlist.LeaveByTokenAsync(req.Token) ? NoContent() : Invalid();
}
