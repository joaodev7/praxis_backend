using Praxis.Domain.Common;

namespace Praxis.Domain.Entities;

public class PlanFeature : BaseEntity
{
    public Guid PlanId { get; set; }
    public Plan Plan { get; set; } = null!;
    public string FeatureCode { get; set; } = string.Empty; // "advanced_analytics", "period_comparison", "excel_export", "custom_reports", "priority_support"
    public bool IsEnabled { get; set; } = true;
}
