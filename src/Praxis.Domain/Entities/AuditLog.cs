using Praxis.Domain.Common;

namespace Praxis.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? Metadata { get; set; }
}
