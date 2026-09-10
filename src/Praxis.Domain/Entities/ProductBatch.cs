using Praxis.Domain.Common;

namespace Praxis.Domain.Entities;

public class ProductBatch : BaseEntity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public string BatchCode { get; set; } = string.Empty;
    public string? OriginalBatchCode { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? OriginalExpirationDate { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<FoodLabel> FoodLabels { get; set; } = new List<FoodLabel>();
}
