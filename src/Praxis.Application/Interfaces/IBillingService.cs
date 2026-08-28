using Praxis.Application.DTOs.Billing;

namespace Praxis.Application.Interfaces;

public interface IBillingService
{
    Task<List<PlanDto>> GetPublicPlansAsync(CancellationToken ct = default);
    Task<SubscriptionInfoDto> GetSubscriptionAsync(CancellationToken ct = default);
    Task<CheckoutResponseDto> CreateCheckoutAsync(CheckoutRequestDto request, CancellationToken ct = default);
    Task<SubscriptionInfoDto> UpgradePlanAsync(UpgradePlanRequestDto request, CancellationToken ct = default);
    Task<SubscriptionInfoDto> DowngradePlanAsync(DowngradePlanRequestDto request, CancellationToken ct = default);
    Task<SubscriptionInfoDto> CancelSubscriptionAsync(CancellationToken ct = default);
    Task<SubscriptionInfoDto> ReactivateSubscriptionAsync(CancellationToken ct = default);
    Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(CancellationToken ct = default);
}
