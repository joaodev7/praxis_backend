using Praxis.Domain.Common;

namespace Praxis.Domain.Entities;

public class Plan : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // "essential", "professional", "enterprise"
    public string Description { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public int MaxNutritionists { get; set; }
    public int MaxClientCompanies { get; set; }
    public int MaxStorageMb { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<PlanFeature> Features { get; set; } = new List<PlanFeature>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
