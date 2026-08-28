using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class Evidence : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid NonConformityId { get; set; }
    public NonConformity? NonConformity { get; set; }

    public EvidenceType Type { get; set; } = EvidenceType.Photo;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? UploadedByUserId { get; set; }
}
