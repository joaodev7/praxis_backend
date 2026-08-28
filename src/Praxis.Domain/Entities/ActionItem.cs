using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class ActionItem : BaseEntity, ISoftDeletable
{
    public Guid NonConformityId { get; set; }
    public NonConformity? NonConformity { get; set; }

    public string Description { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }
    public User? ResponsibleUser { get; set; }
    public DateTime? DueDate { get; set; }
    public ActionItemStatus Status { get; set; } = ActionItemStatus.Pendente;
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
