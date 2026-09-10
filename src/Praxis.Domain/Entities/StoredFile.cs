using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class StoredFile : BaseEntity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid? UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }

    public Guid? ClientId { get; set; }
    public ClientCompany? Client { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public FileCategory Category { get; set; } = FileCategory.Other;
    public FileStatus Status { get; set; } = FileStatus.Pending;
    public DateTime? UploadedAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
