using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class Payment : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid SubscriptionId { get; set; }
    public Subscription Subscription { get; set; } = null!;

    public string? ProviderPaymentId { get; set; } // Asaas payment ID
    public string? ProviderPaymentLinkId { get; set; } // Asaas checkout / payment link ID
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime? DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.Pix;
    public string Provider { get; set; } = "Asaas";
    public string? InvoiceUrl { get; set; }
    public string? PixQrCodeUrl { get; set; }
    public string? PixCopyPasteCode { get; set; }
    public string? CardBrand { get; set; }
    public string? CardLastFour { get; set; }
}
