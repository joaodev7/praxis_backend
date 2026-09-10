using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class ValidityRule : BaseEntity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>
    /// Se informado, a regra é específica para a unidade (Prioridade 2 ou 5).
    /// Se nulo, aplica-se a todas as unidades do tenant.
    /// </summary>
    public Guid? UnitId { get; set; }
    public Unit? Unit { get; set; }

    /// <summary>
    /// Se informado, a regra é específica para um determinado produto (Prioridade 1).
    /// </summary>
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProductCategory { get; set; }

    public LabelType? LabelType { get; set; }
    public LabelOperationType? OperationType { get; set; }
    public StorageCondition? StorageCondition { get; set; }

    public decimal? MaximumTemperature { get; set; }

    public int ValidityValue { get; set; }
    public ValidityUnit ValidityUnit { get; set; } = ValidityUnit.Days;

    public bool AllowManualExpiration { get; set; }
    public bool RequiresTechnicalBasis { get; set; }
    public string? TechnicalBasis { get; set; }
    public string? RegulatoryReference { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<FoodLabel> FoodLabels { get; set; } = new List<FoodLabel>();
}
