using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class Subscription : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? GracePeriodEndsAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool EndsAtPeriodEnd { get; set; } = false;

    public string PaymentProvider { get; set; } = "Asaas";
    public string? ProviderCustomerId { get; set; }
    public string? ProviderSubscriptionId { get; set; }

    public decimal? CustomPrice { get; set; }

    public ICollection<SubscriptionFeatureOverride> Overrides { get; set; } = new List<SubscriptionFeatureOverride>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
