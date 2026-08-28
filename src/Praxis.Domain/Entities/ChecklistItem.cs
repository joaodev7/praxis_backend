using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class ChecklistItem : BaseEntity, ISoftDeletable
{
    public Guid ChecklistId { get; set; }
    public Checklist? Checklist { get; set; }

    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool Required { get; set; } = true;
    public CommonStatus Status { get; set; } = CommonStatus.Active;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
