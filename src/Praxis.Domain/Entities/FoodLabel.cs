using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class FoodLabel : BaseEntity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid UnitId { get; set; }
    public Unit? Unit { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    public Guid? ValidityRuleId { get; set; }
    public ValidityRule? ValidityRule { get; set; }

    public LabelType LabelType { get; set; }
    public LabelOperationType OperationType { get; set; }
    public LabelStatus Status { get; set; } = LabelStatus.Active;

    public string Description { get; set; } = string.Empty;
    public string InternalBatchCode { get; set; } = string.Empty;

    public DateTime? ManufacturedAt { get; set; }
    public DateTime? PreparedAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? PortionedAt { get; set; }
    public DateTime ValidityStartAt { get; set; }

    public DateTime CalculatedExpirationDate { get; set; }
    public DateTime? ManualExpirationDate { get; set; }

    public ValiditySource ValiditySource { get; set; } = ValiditySource.Rule;
    public string? ValidityJustification { get; set; }

    public StorageCondition StorageCondition { get; set; } = StorageCondition.Refrigerated;
    public decimal? StorageTemperatureMin { get; set; }
    public decimal? StorageTemperatureMax { get; set; }
    public string? StorageInstructions { get; set; }

    /// <summary>
    /// Token aleatório e seguro para verificação pública via QR Code.
    /// </summary>
    public string PublicToken { get; set; } = string.Empty;

    public int PrintCount { get; set; } = 0;
    public DateTime? LastPrintedAt { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public Guid? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public Guid? DiscardedByUserId { get; set; }
    public User? DiscardedByUser { get; set; }
    public DateTime? DiscardedAt { get; set; }
    public string? DiscardReason { get; set; }
    public decimal? DiscardQuantity { get; set; }
    public string? DiscardUnit { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<LabelPrint> Prints { get; set; } = new List<LabelPrint>();
    public ICollection<FoodLabelAudit> Audits { get; set; } = new List<FoodLabelAudit>();

    /// <summary>
    /// Retorna a data efetiva de validade (Manual, se informada, ou Calculada).
    /// </summary>
    public DateTime EffectiveExpirationDate => ManualExpirationDate ?? CalculatedExpirationDate;

    /// <summary>
    /// Verifica se o alimento está vencido na data atual.
    /// </summary>
    public bool IsExpired => Status == LabelStatus.Active && EffectiveExpirationDate < DateTime.UtcNow;
}
