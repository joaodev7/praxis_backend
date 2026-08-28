using Praxis.Domain.Common;

namespace Praxis.Domain.Entities;

public class PaymentWebhookEvent : BaseEntity
{
    public string Provider { get; set; } = "Asaas";
    public string ProviderEventId { get; set; } = string.Empty; // Unique webhook event ID
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string Status { get; set; } = "Received"; // "Received", "Processed", "Failed", "Ignored"
    public string? Error { get; set; }
}
