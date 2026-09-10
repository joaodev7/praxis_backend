using Praxis.Domain.Common;

namespace Praxis.Domain.Entities;

public class Product : BaseEntity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>
    /// Se nulo, o produto pertence ao catálogo geral da empresa de consultoria (Tenant).
    /// Se preenchido, é um produto exclusivo desta unidade operacional.
    /// </summary>
    public Guid? UnitId { get; set; }
    public Unit? Unit { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<ProductBatch> Batches { get; set; } = new List<ProductBatch>();
    public ICollection<FoodLabel> FoodLabels { get; set; } = new List<FoodLabel>();
}
