using FluentAssertions;
using Praxis.Domain.Enums;
using Praxis.Infrastructure.Billing.PaymentProviders.Asaas;
using Xunit;

namespace Praxis.Billing.Tests;

public class StatusMapperTests
{
    [Theory]
    [InlineData("PENDING", PaymentStatus.Pending)]
    [InlineData("AWAITING_RISK_ANALYSIS", PaymentStatus.Pending)]
    [InlineData("RECEIVED", PaymentStatus.Confirmed)]
    [InlineData("CONFIRMED", PaymentStatus.Confirmed)]
    [InlineData("RECEIVED_IN_CASH", PaymentStatus.Confirmed)]
    [InlineData("OVERDUE", PaymentStatus.Overdue)]
    [InlineData("REFUNDED", PaymentStatus.Refunded)]
    [InlineData("REFUND_REQUESTED", PaymentStatus.Refunded)]
    [InlineData("CHARGEBACK_REQUESTED", PaymentStatus.Failed)]
    [InlineData("DELETED", PaymentStatus.Cancelled)]
    [InlineData("", PaymentStatus.Pending)]
    [InlineData(null, PaymentStatus.Pending)]
    public void MapPaymentStatus_ShouldCorrectlyMapAsaasStatus(string? asaasStatus, PaymentStatus expected)
    {
        var result = AsaasStatusMapper.MapPaymentStatus(asaasStatus);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("ACTIVE", SubscriptionStatus.Active)]
    [InlineData("EXPIRED", SubscriptionStatus.Expired)]
    [InlineData("INACTIVE", SubscriptionStatus.Suspended)]
    [InlineData("", SubscriptionStatus.Active)]
    [InlineData(null, SubscriptionStatus.Active)]
    public void MapSubscriptionStatus_ShouldCorrectlyMapAsaasStatus(string? asaasStatus, SubscriptionStatus expected)
    {
        var result = AsaasStatusMapper.MapSubscriptionStatus(asaasStatus);
        result.Should().Be(expected);
    }
}
