using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praxis.Application.DTOs.Billing;
using Praxis.Application.Interfaces;

namespace Praxis.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;

    public BillingController(IBillingService billingService)
    {
        _billingService = billingService;
    }

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlans(CancellationToken ct)
    {
        var plans = await _billingService.GetPublicPlansAsync(ct);
        return Ok(plans);
    }

    [HttpGet("subscription")]
    [Authorize]
    public async Task<IActionResult> GetSubscription(CancellationToken ct)
    {
        var sub = await _billingService.GetSubscriptionAsync(ct);
        return Ok(sub);
    }

    [HttpPost("checkout")]
    [Authorize]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequestDto request, CancellationToken ct)
    {
        var response = await _billingService.CreateCheckoutAsync(request, ct);
        return Ok(response);
    }

    [HttpPost("subscription/upgrade")]
    [Authorize]
    public async Task<IActionResult> Upgrade([FromBody] UpgradePlanRequestDto request, CancellationToken ct)
    {
        var updatedSub = await _billingService.UpgradePlanAsync(request, ct);
        return Ok(updatedSub);
    }

    [HttpPost("subscription/downgrade")]
    [Authorize]
    public async Task<IActionResult> Downgrade([FromBody] DowngradePlanRequestDto request, CancellationToken ct)
    {
        var updatedSub = await _billingService.DowngradePlanAsync(request, ct);
        return Ok(updatedSub);
    }

    [HttpPost("subscription/cancel")]
    [Authorize]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        var updatedSub = await _billingService.CancelSubscriptionAsync(ct);
        return Ok(updatedSub);
    }

    [HttpPost("subscription/reactivate")]
    [Authorize]
    public async Task<IActionResult> Reactivate(CancellationToken ct)
    {
        var updatedSub = await _billingService.ReactivateSubscriptionAsync(ct);
        return Ok(updatedSub);
    }

    [HttpGet("payments")]
    [Authorize]
    public async Task<IActionResult> GetPayments(CancellationToken ct)
    {
        var payments = await _billingService.GetPaymentHistoryAsync(ct);
        return Ok(payments);
    }
}
