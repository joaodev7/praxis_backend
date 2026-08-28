using Praxis.Application.DTOs.Billing;

namespace Praxis.Application.Interfaces;

public interface IEntitlementService
{
    Task<bool> HasFeatureAsync(Guid tenantId, string featureCode, CancellationToken ct = default);
    Task ValidateLimitAsync(Guid tenantId, string limitCode, int requestedQuantity = 1, CancellationToken ct = default);
    Task<SubscriptionInfoDto> GetCurrentSubscriptionAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> HasActiveAccessAsync(Guid tenantId, CancellationToken ct = default);
}
