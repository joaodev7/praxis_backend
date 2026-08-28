using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Praxis.Application.DTOs.Billing;
using Praxis.Application.Interfaces;
using Praxis.Application.Services;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using Praxis.Infrastructure.Data;
using Xunit;

namespace Praxis.Billing.Tests;

public class BillingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IPaymentGateway> _paymentGatewayMock;
    private readonly EntitlementService _entitlementService;
    private readonly BillingService _sut;
    private readonly Guid _tenantId;
    private readonly IDisposable _connection;

    public BillingServiceTests()
    {
        _tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(_tenantId);
        _context = context;
        _currentUserMock = currentUserMock;
        _connection = connection;

        _paymentGatewayMock = new Mock<IPaymentGateway>();
        _entitlementService = new EntitlementService(_context);
        _sut = new BillingService(_context, _currentUserMock.Object, _paymentGatewayMock.Object, _entitlementService);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetPublicPlansAsync_ShouldReturnActivePlansOrderedByPrice()
    {
        var plans = await _sut.GetPublicPlansAsync();

        plans.Should().NotBeNull();
        plans.Should().HaveCount(3);
        plans[0].Code.Should().Be("enterprise");
        plans[1].Code.Should().Be("essential");
        plans[2].Code.Should().Be("professional");
    }

    [Fact]
    public async Task CreateCheckoutAsync_Pix_ShouldCreateCustomer_CreateSubscription_AndReturnPixData()
    {
        // Arrange
        const string mockCustomerId = "cus_mock_12345";
        const string mockSubscriptionId = "sub_mock_67890";
        const string mockPaymentId = "pay_mock_11111";

        _paymentGatewayMock.Setup(g => g.GetOrCreateCustomerAsync(It.IsAny<PaymentCustomer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayCustomerResult
            {
                ProviderCustomerId = mockCustomerId,
                Success = true
            });

        _paymentGatewayMock.Setup(g => g.CreateSubscriptionAsync(It.IsAny<CreateGatewaySubscriptionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewaySubscriptionResult
            {
                ProviderSubscriptionId = mockSubscriptionId,
                ProviderPaymentId = mockPaymentId,
                Status = PaymentStatus.Pending,
                Value = 299.00m,
                NextDueDate = DateTime.UtcNow.AddDays(3),
                InvoiceUrl = "https://sandbox.asaas.com/i/11111",
                Success = true
            });

        _paymentGatewayMock.Setup(g => g.GetPixQrCodeAsync(mockPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPixQrCodeResult
            {
                EncodedImage = "iVBORw0KGgoAAAANSUhEUgAA...",
                Payload = "00020126580014br.gov.bcb.pix...",
                Success = true
            });

        var request = new CheckoutRequestDto
        {
            PlanCode = "professional",
            BillingCycle = BillingCycle.Monthly,
            PaymentMethod = PaymentMethodType.Pix
        };

        // Act
        var result = await _sut.CreateCheckoutAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(PaymentStatus.Pending);
        result.Amount.Should().Be(299.00m);
        result.Pix.Should().NotBeNull();
        result.Pix!.QrCodeUrl.Should().Be("iVBORw0KGgoAAAANSUhEUgAA...");
        result.Pix!.CopyPasteCode.Should().Be("00020126580014br.gov.bcb.pix...");

        // Verify DB persistence
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.ProviderPaymentId == mockPaymentId);
        payment.Should().NotBeNull();
        payment!.Amount.Should().Be(299.00m);
        payment.PixCopyPasteCode.Should().Be("00020126580014br.gov.bcb.pix...");

        var sub = await _context.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == _tenantId);
        sub.Should().NotBeNull();
        sub!.ProviderCustomerId.Should().Be(mockCustomerId);
        sub.ProviderSubscriptionId.Should().Be(mockSubscriptionId);
    }

    [Fact]
    public async Task CreateCheckoutAsync_SubsequentCheckout_ShouldReuseExistingCustomer()
    {
        // Arrange
        const string mockCustomerId = "cus_mock_reuse_999";
        _paymentGatewayMock.Setup(g => g.GetOrCreateCustomerAsync(It.IsAny<PaymentCustomer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayCustomerResult
            {
                ProviderCustomerId = mockCustomerId,
                Success = true
            });

        _paymentGatewayMock.Setup(g => g.CreateSubscriptionAsync(It.IsAny<CreateGatewaySubscriptionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewaySubscriptionResult
            {
                ProviderSubscriptionId = "sub_999",
                ProviderPaymentId = "pay_999",
                Status = PaymentStatus.Pending,
                Value = 149.00m,
                Success = true
            });

        var request = new CheckoutRequestDto
        {
            PlanCode = "essential",
            BillingCycle = BillingCycle.Monthly,
            PaymentMethod = PaymentMethodType.Pix
        };

        // 1st checkout
        await _sut.CreateCheckoutAsync(request);

        // 2nd checkout
        await _sut.CreateCheckoutAsync(request);

        // Assert GetOrCreateCustomerAsync was called with the same customer information
        _paymentGatewayMock.Verify(g => g.GetOrCreateCustomerAsync(It.Is<PaymentCustomer>(c => c.ExternalReference == _tenantId.ToString()), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpgradePlanAsync_ShouldUpdatePlanAndNotifyGateway()
    {
        // Arrange
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        var essential = await _context.Plans.FirstAsync(p => p.Code == "essential");
        sub.PlanId = essential.Id;
        sub.ProviderSubscriptionId = "sub_upgrade_123";
        await _context.SaveChangesAsync();

        _paymentGatewayMock.Setup(g => g.ChangeSubscriptionAsync(It.IsAny<ChangeGatewaySubscriptionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewaySubscriptionResult
            {
                ProviderSubscriptionId = "sub_upgrade_123",
                Status = PaymentStatus.Confirmed,
                Value = 299.00m,
                Success = true
            });

        // Act
        var result = await _sut.UpgradePlanAsync(new UpgradePlanRequestDto
        {
            NewPlanCode = "professional",
            BillingCycle = BillingCycle.Monthly
        });

        // Assert
        result.PlanCode.Should().Be("professional");
        result.MaxNutritionists.Should().Be(10);
        result.MaxClientCompanies.Should().Be(50);
        _paymentGatewayMock.Verify(g => g.ChangeSubscriptionAsync(It.Is<ChangeGatewaySubscriptionRequest>(r => r.ProviderSubscriptionId == "sub_upgrade_123" && r.Value == 299.00m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DowngradePlanAsync_ShouldBlock_WhenCurrentEntitiesExceedTargetPlanLimits()
    {
        // Arrange: Tenant has 15 clients (Essential allows max 10)
        for (int i = 0; i < 15; i++)
        {
            _context.ClientCompanies.Add(new ClientCompany
            {
                TenantId = _tenantId,
                LegalName = $"Cliente {i}",
                TradeName = $"Cliente {i}",
                Cnpj = $"00.000.000/0001-{i:D2}",
                Status = CommonStatus.Active
            });
        }
        await _context.SaveChangesAsync();

        // Act & Assert
        var act = () => _sut.DowngradePlanAsync(new DowngradePlanRequestDto { NewPlanCode = "essential" });
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Não é possível alterar para o plano PRAXIS Essencial*15 clientes*");
    }

    [Fact]
    public async Task CancelSubscriptionAsync_ShouldSetEndsAtPeriodEndAndCancelledStatus()
    {
        // Arrange
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        sub.ProviderSubscriptionId = "sub_to_cancel";
        sub.CurrentPeriodEnd = DateTime.UtcNow.AddDays(20);
        await _context.SaveChangesAsync();

        _paymentGatewayMock.Setup(g => g.CancelSubscriptionAsync("sub_to_cancel", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CancelSubscriptionAsync();

        // Assert
        result.Status.Should().Be(SubscriptionStatus.Cancelled);
        result.CancelledAtPeriodEnd.Should().BeTrue();
        result.HasAccess.Should().BeTrue(); // Remains accessible until CurrentPeriodEnd
    }

    [Fact]
    public async Task ReactivateSubscriptionAsync_ShouldRestoreActiveStatus()
    {
        // Arrange
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        sub.Status = SubscriptionStatus.Cancelled;
        sub.EndsAtPeriodEnd = true;
        sub.CancelledAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.ReactivateSubscriptionAsync();

        // Assert
        result.Status.Should().Be(SubscriptionStatus.Active);
        result.CancelledAtPeriodEnd.Should().BeFalse();
        result.HasAccess.Should().BeTrue();
    }
}
