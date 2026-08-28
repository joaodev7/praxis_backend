using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praxis.Application.Interfaces;

namespace Praxis.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public class PaymentWebhookController : ControllerBase
{
    private readonly IAsaasWebhookService _webhookService;
    private readonly ILogger<PaymentWebhookController> _logger;

    public PaymentWebhookController(IAsaasWebhookService webhookService, ILogger<PaymentWebhookController> logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    [HttpPost("asaas")]
    [AllowAnonymous]
    public async Task<IActionResult> AsaasWebhook(CancellationToken ct)
    {
        // Extract token from all possible headers or query params
        string tokenHeader = string.Empty;
        if (Request.Headers.TryGetValue("asaas-access-token", out var h1)) tokenHeader = h1.ToString();
        else if (Request.Headers.TryGetValue("access_token", out var h2)) tokenHeader = h2.ToString();
        else if (Request.Headers.TryGetValue("x-asaas-access-token", out var h3)) tokenHeader = h3.ToString();
        else if (Request.Headers.TryGetValue("Authorization", out var h4)) tokenHeader = h4.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
        else if (Request.Query.TryGetValue("token", out var q1)) tokenHeader = q1.ToString();
        else if (Request.Query.TryGetValue("accessToken", out var q2)) tokenHeader = q2.ToString();

        using var reader = new StreamReader(Request.Body);
        string payload = await reader.ReadToEndAsync(ct);

        _logger.LogInformation("Received webhook from Asaas. Token: {TokenMasked}, Payload length: {Length}", 
            string.IsNullOrEmpty(tokenHeader) ? "EMPTY" : $"{tokenHeader[..Math.Min(8, tokenHeader.Length)]}***", 
            payload.Length);

        // Ping / URL Verification Check from Asaas (empty body or test ping)
        if (string.IsNullOrWhiteSpace(payload) || payload.Trim() == "{}" || payload.Trim() == "[]")
        {
            _logger.LogInformation("Asaas URL verification ping received and acknowledged.");
            return Ok(new { status = "SUCCESS", message = "Asaas Webhook verification ping successful." });
        }

        try
        {
            await _webhookService.ProcessWebhookAsync(tokenHeader, payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Asaas webhook event");
        }

        // Asaas strictly requires HTTP 200 OK to prevent queue interruption
        return Ok(new { status = "SUCCESS", message = "Webhook processed" });
    }
}
