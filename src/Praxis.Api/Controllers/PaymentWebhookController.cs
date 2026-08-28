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
        string tokenHeader = Request.Headers["asaas-access-token"].ToString();
        if (string.IsNullOrWhiteSpace(tokenHeader))
        {
            tokenHeader = Request.Headers["access_token"].ToString();
        }

        using var reader = new StreamReader(Request.Body);
        string payload = await reader.ReadToEndAsync(ct);

        _logger.LogInformation("Received webhook from Asaas. Payload length: {Length}", payload.Length);

        bool processed = await _webhookService.ProcessWebhookAsync(tokenHeader, payload, ct);

        if (!processed)
        {
            return BadRequest(new { message = "Webhook rejection or processing error." });
        }

        return Ok(new { message = "Webhook processed successfully." });
    }
}
