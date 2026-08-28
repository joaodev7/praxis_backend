using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class ART : BaseEntity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid UnitId { get; set; }
    public Unit? Unit { get; set; }

    public Guid NutritionistId { get; set; }
    public Nutritionist? Nutritionist { get; set; }

    public string Number { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ArtStatus Status { get; set; } = ArtStatus.Active;
    public string? DocumentUrl { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
