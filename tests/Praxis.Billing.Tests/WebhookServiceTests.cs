using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using Praxis.Infrastructure.Billing.PaymentProviders.Asaas;
using Praxis.Infrastructure.Data;
using Xunit;

namespace Praxis.Billing.Tests;

public class WebhookServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly AsaasWebhookService _sut;
    private readonly Guid _tenantId;
    private readonly IDisposable _connection;
    private const string WebhookToken = "whsec_test_token_123456";

    public WebhookServiceTests()
    {
        _tenantId = Guid.NewGuid();
        var (context, _, connection) = TestDbContextFactory.CreateInMemoryDbContext(_tenantId);
        _context = context;
        _connection = connection;

        var optionsMock = new Mock<IOptions<AsaasOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new AsaasOptions
        {
            WebhookToken = WebhookToken
        });

        var loggerMock = new Mock<ILogger<AsaasWebhookService>>();
        _sut = new AsaasWebhookService(_context, optionsMock.Object, loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ProcessWebhookAsync_PaymentConfirmed_ShouldActivateSubscriptionAndExtendPeriod()
    {
        // Arrange
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        sub.Status = SubscriptionStatus.Trial;

        var payment = new Payment
        {
            TenantId = _tenantId,
            SubscriptionId = sub.Id,
            ProviderPaymentId = "pay_test_confirmed_1",
            Amount = 299.00m,
            Status = PaymentStatus.Pending,
            PaymentMethod = PaymentMethodType.Pix
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        var payload = @"{
            ""id"": ""evt_conf_001"",
            ""event"": ""PAYMENT_CONFIRMED"",
            ""payment"": {
                ""id"": ""pay_test_confirmed_1"",
                ""customer"": ""cus_123"",
                ""status"": ""CONFIRMED"",
                ""value"": 299.00
            }
        }";

        // Act
        var result = await _sut.ProcessWebhookAsync(WebhookToken, payload);

        // Assert
        result.Should().BeTrue();

        var updatedPayment = await _context.Payments.FirstAsync(p => p.Id == payment.Id);
        updatedPayment.Status.Should().Be(PaymentStatus.Confirmed);
        updatedPayment.PaidAt.Should().NotBeNull();

        var updatedSub = await _context.Subscriptions.FirstAsync(s => s.Id == sub.Id);
        updatedSub.Status.Should().Be(SubscriptionStatus.Active);
        updatedSub.GracePeriodEndsAt.Should().BeNull();
        updatedSub.CurrentPeriodEnd.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task ProcessWebhookAsync_PaymentOverdue_ShouldSetSubscriptionToPastDueWith7DaysGracePeriod()
    {
        // Arrange
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        sub.Status = SubscriptionStatus.Active;

        var payment = new Payment
        {
            TenantId = _tenantId,
            SubscriptionId = sub.Id,
            ProviderPaymentId = "pay_test_overdue_1",
            Amount = 299.00m,
            Status = PaymentStatus.Pending,
            PaymentMethod = PaymentMethodType.CreditCard
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        var payload = @"{
            ""id"": ""evt_overdue_001"",
            ""event"": ""PAYMENT_OVERDUE"",
            ""payment"": {
                ""id"": ""pay_test_overdue_1"",
                ""customer"": ""cus_123"",
                ""status"": ""OVERDUE"",
                ""value"": 299.00
            }
        }";

        // Act
        var result = await _sut.ProcessWebhookAsync(WebhookToken, payload);

        // Assert
        result.Should().BeTrue();

        var updatedPayment = await _context.Payments.FirstAsync(p => p.Id == payment.Id);
        updatedPayment.Status.Should().Be(PaymentStatus.Overdue);

        var updatedSub = await _context.Subscriptions.FirstAsync(s => s.Id == sub.Id);
        updatedSub.Status.Should().Be(SubscriptionStatus.PastDue);
        updatedSub.GracePeriodEndsAt.Should().NotBeNull();
        updatedSub.GracePeriodEndsAt.Should().BeAfter(DateTime.UtcNow.AddDays(6)); // ~7 days
    }

    [Fact]
    public async Task ProcessWebhookAsync_DuplicateWebhook_ShouldBeIdempotent()
    {
        // Arrange
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        var payment = new Payment
        {
            TenantId = _tenantId,
            SubscriptionId = sub.Id,
            ProviderPaymentId = "pay_test_idempotent_1",
            Amount = 299.00m,
            Status = PaymentStatus.Pending,
            PaymentMethod = PaymentMethodType.Pix
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        var payload = @"{
            ""id"": ""evt_idempotent_001"",
            ""event"": ""PAYMENT_CONFIRMED"",
            ""payment"": {
                ""id"": ""pay_test_idempotent_1"",
                ""status"": ""CONFIRMED"",
                ""value"": 299.00
            }
        }";

        // Act: 1st delivery
        var result1 = await _sut.ProcessWebhookAsync(WebhookToken, payload);
        var originalPeriodEnd = (await _context.Subscriptions.FirstAsync(s => s.Id == sub.Id)).CurrentPeriodEnd;

        // Act: 2nd delivery (duplicate)
        var result2 = await _sut.ProcessWebhookAsync(WebhookToken, payload);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();

        var webhookEvents = await _context.PaymentWebhookEvents.Where(e => e.ProviderEventId == "evt_idempotent_001").ToListAsync();
        webhookEvents.Should().HaveCount(1); // Exactly 1 event recorded
        webhookEvents[0].Status.Should().Be("Processed");

        var currentPeriodEnd = (await _context.Subscriptions.FirstAsync(s => s.Id == sub.Id)).CurrentPeriodEnd;
        currentPeriodEnd.Should().Be(originalPeriodEnd); // Period was not extended again
    }

    [Fact]
    public async Task ProcessWebhookAsync_CheckoutPaid_WithPaymentLink_ShouldCreatePaymentAndActivateSubscription()
    {
        // Arrange: Subscription with ProviderPaymentLinkId
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        sub.Status = SubscriptionStatus.Trial;
        sub.ProviderPaymentLinkId = "plk_test_webhook_123";
        await _context.SaveChangesAsync();

        var payload = @"{
            ""id"": ""evt_plk_conf_001"",
            ""event"": ""PAYMENT_CONFIRMED"",
            ""payment"": {
                ""id"": ""pay_from_checkout_999"",
                ""customer"": ""cus_checkout_123"",
                ""paymentLink"": ""plk_test_webhook_123"",
                ""billingType"": ""CREDIT_CARD"",
                ""status"": ""CONFIRMED"",
                ""value"": 299.00,
                ""invoiceUrl"": ""https://sandbox.asaas.com/i/invoice999""
            }
        }";

        // Act
        var result = await _sut.ProcessWebhookAsync(WebhookToken, payload);

        // Assert
        result.Should().BeTrue();

        var createdPayment = await _context.Payments.FirstOrDefaultAsync(p => p.ProviderPaymentId == "pay_from_checkout_999");
        createdPayment.Should().NotBeNull();
        createdPayment!.Status.Should().Be(PaymentStatus.Confirmed);
        createdPayment.PaymentMethod.Should().Be(PaymentMethodType.CreditCard);
        createdPayment.ProviderPaymentLinkId.Should().Be("plk_test_webhook_123");
        createdPayment.InvoiceUrl.Should().Be("https://sandbox.asaas.com/i/invoice999");
        createdPayment.PaidAt.Should().NotBeNull();

        var updatedSub = await _context.Subscriptions.FirstAsync(s => s.Id == sub.Id);
        updatedSub.Status.Should().Be(SubscriptionStatus.Active);
        updatedSub.GracePeriodEndsAt.Should().BeNull();
        updatedSub.CurrentPeriodEnd.Should().BeAfter(DateTime.UtcNow);
    }
}
