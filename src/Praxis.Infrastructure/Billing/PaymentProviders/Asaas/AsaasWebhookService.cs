using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Infrastructure.Billing.PaymentProviders.Asaas;

public class AsaasWebhookService : IAsaasWebhookService
{
    private readonly IApplicationDbContext _context;
    private readonly AsaasOptions _options;
    private readonly ILogger<AsaasWebhookService> _logger;

    public AsaasWebhookService(
        IApplicationDbContext context,
        IOptions<AsaasOptions> options,
        ILogger<AsaasWebhookService> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> ProcessWebhookAsync(string webhookTokenHeader, string payloadJson, CancellationToken ct = default)
    {
        // 1. Validate Webhook Token if configured
        if (!string.IsNullOrWhiteSpace(_options.WebhookToken))
        {
            if (string.IsNullOrWhiteSpace(webhookTokenHeader) || !webhookTokenHeader.Equals(_options.WebhookToken, StringComparison.Ordinal))
            {
                _logger.LogWarning("Asaas Webhook rejected: Invalid token header '{Token}'", webhookTokenHeader);
                return false;
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            string eventId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
            string eventType = root.TryGetProperty("event", out var evtProp) ? evtProp.GetString() ?? "UNKNOWN" : "UNKNOWN";

            _logger.LogInformation("Asaas Webhook received: Event {EventId}, Type {EventType}", eventId, eventType);

            // 2. Check Idempotency
            var existingEvent = await _context.PaymentWebhookEvents
                .FirstOrDefaultAsync(e => e.Provider == "Asaas" && e.ProviderEventId == eventId, ct);

            if (existingEvent != null && existingEvent.Status == "Processed")
            {
                _logger.LogInformation("Asaas Webhook {EventId} already processed. Skipping.", eventId);
                return true;
            }

            var webhookLog = existingEvent ?? new PaymentWebhookEvent
            {
                Provider = "Asaas",
                ProviderEventId = eventId,
                EventType = eventType,
                Payload = payloadJson,
                ReceivedAt = DateTime.UtcNow,
                Status = "Received"
            };

            if (existingEvent == null)
            {
                _context.PaymentWebhookEvents.Add(webhookLog);
            }

            // 3. Extract Payment object
            if (root.TryGetProperty("payment", out var paymentElement))
            {
                string? asaasPaymentId = paymentElement.TryGetProperty("id", out var pid) ? pid.GetString() : null;
                string? asaasStatus = paymentElement.TryGetProperty("status", out var pstat) ? pstat.GetString() : null;
                string? asaasSubscriptionId = paymentElement.TryGetProperty("subscription", out var psub) ? psub.GetString() : null;
                decimal value = paymentElement.TryGetProperty("value", out var pval) ? pval.GetDecimal() : 0m;

                if (!string.IsNullOrEmpty(asaasPaymentId))
                {
                    var payment = await _context.Payments
                        .Include(p => p.Subscription)
                        .FirstOrDefaultAsync(p => p.ProviderPaymentId == asaasPaymentId, ct);

                    var mappedStatus = AsaasStatusMapper.MapPaymentStatus(asaasStatus);

                    if (payment != null)
                    {
                        payment.Status = mappedStatus;
                        payment.UpdatedAt = DateTime.UtcNow;

                        if (mappedStatus == PaymentStatus.Confirmed)
                        {
                            payment.PaidAt = DateTime.UtcNow;

                            // Activate / extend subscription
                            if (payment.Subscription != null)
                            {
                                payment.Subscription.Status = SubscriptionStatus.Active;
                                payment.Subscription.CurrentPeriodStart = DateTime.UtcNow;
                                payment.Subscription.CurrentPeriodEnd = payment.Subscription.BillingCycle == BillingCycle.Annual
                                    ? DateTime.UtcNow.AddYears(1)
                                    : DateTime.UtcNow.AddMonths(1);
                                payment.Subscription.GracePeriodEndsAt = null;
                                payment.Subscription.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                        else if (mappedStatus == PaymentStatus.Overdue)
                        {
                            if (payment.Subscription != null && payment.Subscription.Status != SubscriptionStatus.Suspended)
                            {
                                payment.Subscription.Status = SubscriptionStatus.PastDue;
                                payment.Subscription.GracePeriodEndsAt = DateTime.UtcNow.AddDays(7);
                                payment.Subscription.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(asaasSubscriptionId))
                    {
                        // Check if subscription exists by ProviderSubscriptionId
                        var subscription = await _context.Subscriptions
                            .FirstOrDefaultAsync(s => s.ProviderSubscriptionId == asaasSubscriptionId, ct);

                        if (subscription != null)
                        {
                            var newPayment = new Payment
                            {
                                TenantId = subscription.TenantId,
                                SubscriptionId = subscription.Id,
                                ProviderPaymentId = asaasPaymentId,
                                Amount = value,
                                Status = mappedStatus,
                                DueDate = DateTime.UtcNow,
                                Provider = "Asaas",
                                PaidAt = mappedStatus == PaymentStatus.Confirmed ? DateTime.UtcNow : null
                            };

                            if (mappedStatus == PaymentStatus.Confirmed)
                            {
                                subscription.Status = SubscriptionStatus.Active;
                                subscription.CurrentPeriodStart = DateTime.UtcNow;
                                subscription.CurrentPeriodEnd = subscription.BillingCycle == BillingCycle.Annual
                                    ? DateTime.UtcNow.AddYears(1)
                                    : DateTime.UtcNow.AddMonths(1);
                                subscription.GracePeriodEndsAt = null;
                                subscription.UpdatedAt = DateTime.UtcNow;
                            }

                            _context.Payments.Add(newPayment);
                        }
                    }
                }
            }

            webhookLog.Status = "Processed";
            webhookLog.ProcessedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Asaas Webhook payload");
            return false;
        }
    }
}
