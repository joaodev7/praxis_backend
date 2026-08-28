using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class Visit : BaseEntity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid UnitId { get; set; }
    public Unit? Unit { get; set; }

    public Guid NutritionistId { get; set; }
    public Nutritionist? Nutritionist { get; set; }

    public Guid? ChecklistId { get; set; }
    public Checklist? Checklist { get; set; }

    public DateTime ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.Scheduled;
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<VisitItem> Items { get; set; } = new List<VisitItem>();
    public ICollection<NonConformity> NonConformities { get; set; } = new List<NonConformity>();
}
