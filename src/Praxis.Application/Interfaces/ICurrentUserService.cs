using Praxis.Domain.Enums;

namespace Praxis.Application.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    UserRole? Role { get; }
    string? UserEmail { get; }
    bool IsAuthenticated { get; }
}
