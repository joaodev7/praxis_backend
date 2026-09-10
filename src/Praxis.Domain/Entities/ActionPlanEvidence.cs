using Praxis.Domain.Common;

namespace Praxis.Domain.Entities;

public class ActionPlanEvidence : BaseEntity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid ActionPlanId { get; set; }
    public ActionItem? ActionPlan { get; set; }

    public string FileUrl { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Guid? UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
