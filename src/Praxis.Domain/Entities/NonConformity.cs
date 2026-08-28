using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class NonConformity : BaseEntity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid VisitId { get; set; }
    public Visit? Visit { get; set; }

    public Guid? VisitItemId { get; set; }
    public VisitItem? VisitItem { get; set; }

    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public NonConformitySeverity Severity { get; set; } = NonConformitySeverity.Media;
    public NonConformityStatus Status { get; set; } = NonConformityStatus.Aberta;
    public DateTime? DueDate { get; set; }
    public string? CorrectiveAction { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<ActionItem> Actions { get; set; } = new List<ActionItem>();
    public ICollection<Evidence> Evidences { get; set; } = new List<Evidence>();
}
