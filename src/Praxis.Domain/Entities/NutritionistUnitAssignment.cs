using Praxis.Domain.Common;

namespace Praxis.Domain.Entities;

public class NutritionistUnitAssignment : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid NutritionistId { get; set; }
    public Nutritionist? Nutritionist { get; set; }

    public Guid UnitId { get; set; }
    public Unit? Unit { get; set; }
}
