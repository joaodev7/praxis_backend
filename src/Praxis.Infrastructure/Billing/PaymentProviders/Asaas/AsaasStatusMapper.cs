using Praxis.Domain.Enums;

namespace Praxis.Infrastructure.Billing.PaymentProviders.Asaas;

public static class AsaasStatusMapper
{
    public static PaymentStatus MapPaymentStatus(string? asaasStatus)
    {
        if (string.IsNullOrWhiteSpace(asaasStatus))
            return PaymentStatus.Pending;

        return asaasStatus.ToUpperInvariant() switch
        {
            "PENDING" => PaymentStatus.Pending,
            "AWAITING_RISK_ANALYSIS" => PaymentStatus.Pending,
            "RECEIVED" => PaymentStatus.Confirmed,
            "CONFIRMED" => PaymentStatus.Confirmed,
            "RECEIVED_IN_CASH" => PaymentStatus.Confirmed,
            "OVERDUE" => PaymentStatus.Overdue,
            "REFUNDED" => PaymentStatus.Refunded,
            "REFUND_REQUESTED" => PaymentStatus.Refunded,
            "CHARGEBACK_REQUESTED" => PaymentStatus.Failed,
            "CHARGEBACK_DISPUTE" => PaymentStatus.Failed,
            "DUNNING_REQUESTED" => PaymentStatus.Overdue,
            "DUNNING_RECEIVED" => PaymentStatus.Confirmed,
            "AWAITING_CHARGEBACK_REVERSAL" => PaymentStatus.Failed,
            "DELETED" => PaymentStatus.Cancelled,
            _ => PaymentStatus.Pending
        };
    }

    public static SubscriptionStatus MapSubscriptionStatus(string? asaasStatus)
    {
        if (string.IsNullOrWhiteSpace(asaasStatus))
            return SubscriptionStatus.Active;

        return asaasStatus.ToUpperInvariant() switch
        {
            "ACTIVE" => SubscriptionStatus.Active,
            "EXPIRED" => SubscriptionStatus.Expired,
            "INACTIVE" => SubscriptionStatus.Suspended,
            _ => SubscriptionStatus.Active
        };
    }
}
