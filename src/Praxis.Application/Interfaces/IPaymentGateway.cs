using Praxis.Application.DTOs.Billing;

namespace Praxis.Application.Interfaces;

public interface IPaymentGateway
{
    Task<GatewayCustomerResult> GetOrCreateCustomerAsync(PaymentCustomer customer, CancellationToken ct = default);
    Task<GatewaySubscriptionResult> CreateSubscriptionAsync(CreateGatewaySubscriptionRequest request, CancellationToken ct = default);
    Task<GatewaySubscriptionResult> ChangeSubscriptionAsync(ChangeGatewaySubscriptionRequest request, CancellationToken ct = default);
    Task CancelSubscriptionAsync(string providerSubscriptionId, CancellationToken ct = default);
    Task<GatewayPaymentResult?> GetPaymentAsync(string providerPaymentId, CancellationToken ct = default);
    Task<GatewayPixQrCodeResult?> GetPixQrCodeAsync(string providerPaymentId, CancellationToken ct = default);
}
