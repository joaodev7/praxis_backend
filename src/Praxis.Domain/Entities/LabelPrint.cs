using Praxis.Domain.Common;

namespace Praxis.Domain.Entities;

public class LabelPrint : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid LabelId { get; set; }
    public FoodLabel? Label { get; set; }

    public Guid PrintedByUserId { get; set; }
    public User? PrintedByUser { get; set; }

    public DateTime PrintedAt { get; set; } = DateTime.UtcNow;
    public int Quantity { get; set; } = 1;
    public string? PrinterName { get; set; }
    public string? TemplateUsed { get; set; }
}
