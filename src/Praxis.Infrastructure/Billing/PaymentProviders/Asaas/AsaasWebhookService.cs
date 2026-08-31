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
        var expectedToken = !string.IsNullOrWhiteSpace(_options.WebhookToken) 
            ? _options.WebhookToken 
            : "whsec_YOBQ3jkUT3as4CMkZSvNUzsxBaT3sRgQDN3fYzhbKzc";

        if (!string.IsNullOrWhiteSpace(expectedToken) && !string.IsNullOrWhiteSpace(webhookTokenHeader))
        {
            if (!webhookTokenHeader.Trim().Equals(expectedToken.Trim(), StringComparison.Ordinal) &&
                !webhookTokenHeader.Trim().Equals(_options.WebhookToken?.Trim(), StringComparison.Ordinal))
            {
                _logger.LogWarning("Asaas Webhook: Received token header '{ReceivedToken}' differs from configured token.", 
                    webhookTokenHeader.Length > 6 ? $"{webhookTokenHeader[..6]}***" : "***");
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
                string? asaasPaymentLinkId = paymentElement.TryGetProperty("paymentLink", out var plk) ? plk.GetString() : null;
                string? asaasCustomerId = paymentElement.TryGetProperty("customer", out var pcus) ? pcus.GetString() : null;
                string? asaasExternalReference = paymentElement.TryGetProperty("externalReference", out var pext) ? pext.GetString() : null;
                string? asaasBillingType = paymentElement.TryGetProperty("billingType", out var pbtype) ? pbtype.GetString() : null;
                string? asaasInvoiceUrl = paymentElement.TryGetProperty("invoiceUrl", out var pinv) ? pinv.GetString() : null;
                decimal value = paymentElement.TryGetProperty("value", out var pval) ? pval.GetDecimal() : 0m;

                var mappedMethod = asaasBillingType?.ToUpperInvariant() switch
                {
                    "CREDIT_CARD" => PaymentMethodType.CreditCard,
                    "BOLETO" => PaymentMethodType.Boleto,
                    _ => PaymentMethodType.Pix
                };

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
                        if (!string.IsNullOrEmpty(asaasInvoiceUrl)) payment.InvoiceUrl = asaasInvoiceUrl;
                        if (!string.IsNullOrEmpty(asaasPaymentLinkId)) payment.ProviderPaymentLinkId = asaasPaymentLinkId;

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
                    else
                    {
                        // Check if subscription exists by SubscriptionId, PaymentLinkId, CustomerId, or ExternalReference (TenantId)
                        Subscription? subscription = null;

                        if (!string.IsNullOrEmpty(asaasSubscriptionId))
                        {
                            subscription = await _context.Subscriptions
                                .FirstOrDefaultAsync(s => s.ProviderSubscriptionId == asaasSubscriptionId, ct);
                        }

                        if (subscription == null && !string.IsNullOrEmpty(asaasPaymentLinkId))
                        {
                            subscription = await _context.Subscriptions
                                .FirstOrDefaultAsync(s => s.ProviderPaymentLinkId == asaasPaymentLinkId, ct);
                        }

                        if (subscription == null && !string.IsNullOrEmpty(asaasCustomerId))
                        {
                            subscription = await _context.Subscriptions
                                .OrderByDescending(s => s.CreatedAt)
                                .FirstOrDefaultAsync(s => s.ProviderCustomerId == asaasCustomerId, ct);
                        }

                        if (subscription == null && Guid.TryParse(asaasExternalReference, out var tenantGuid))
                        {
                            subscription = await _context.Subscriptions
                                .FirstOrDefaultAsync(s => s.TenantId == tenantGuid, ct);
                        }

                        if (subscription != null)
                        {
                            var newPayment = new Payment
                            {
                                TenantId = subscription.TenantId,
                                SubscriptionId = subscription.Id,
                                ProviderPaymentId = asaasPaymentId,
                                ProviderPaymentLinkId = asaasPaymentLinkId,
                                Amount = value,
                                Status = mappedStatus,
                                DueDate = DateTime.UtcNow,
                                PaymentMethod = mappedMethod,
                                Provider = "Asaas",
                                InvoiceUrl = asaasInvoiceUrl,
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
                            else if (mappedStatus == PaymentStatus.Overdue)
                            {
                                if (subscription.Status != SubscriptionStatus.Suspended)
                                {
                                    subscription.Status = SubscriptionStatus.PastDue;
                                    subscription.GracePeriodEndsAt = DateTime.UtcNow.AddDays(7);
                                    subscription.UpdatedAt = DateTime.UtcNow;
                                }
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
