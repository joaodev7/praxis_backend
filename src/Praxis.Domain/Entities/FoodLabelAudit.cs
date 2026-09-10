using Praxis.Domain.Common;

namespace Praxis.Domain.Entities;

public class FoodLabelAudit : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid LabelId { get; set; }
    public FoodLabel? Label { get; set; }

    public string Action { get; set; } = string.Empty; // "Created", "Updated", "Printed", "Reprinted", "Cancelled", "Discarded"
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string? Details { get; set; }
}
