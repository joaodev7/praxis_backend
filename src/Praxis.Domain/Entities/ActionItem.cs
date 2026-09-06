using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class ActionItem : BaseEntity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid NonConformityId { get; set; }
    public NonConformity? NonConformity { get; set; }

    // 5W2H Model
    public string What { get; set; } = string.Empty;
    public string Description
    {
        get => string.IsNullOrWhiteSpace(_description) ? What : _description;
        set
        {
            _description = value;
            if (string.IsNullOrWhiteSpace(What))
                What = value;
        }
    }
    private string _description = string.Empty;

    public string? Why { get; set; }
    public Guid? ResponsibleUserId { get; set; }
    public User? ResponsibleUser { get; set; }
    public string? ResponsibleName { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Where { get; set; }
    public string? How { get; set; }
    public decimal? HowMuch { get; set; }

    public ActionPlanPriority Priority { get; set; } = ActionPlanPriority.Media;
    public ActionItemStatus Status { get; set; } = ActionItemStatus.Pendente;

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public Guid? ValidatedByUserId { get; set; }
    public User? ValidatedByUser { get; set; }
    public string? ValidationComment { get; set; }
    public string? CancellationReason { get; set; }
    public string? Notes { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<ActionPlanEvidence> Evidences { get; set; } = new List<ActionPlanEvidence>();
}
