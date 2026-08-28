using Praxis.Domain.Common;

namespace Praxis.Domain.Entities;

public class SubscriptionFeatureOverride : BaseEntity
{
    public Guid SubscriptionId { get; set; }
    public Subscription Subscription { get; set; } = null!;

    public string FeatureCode { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string? CustomValue { get; set; } // For numeric limits like "max_nutritionists=35"
}
