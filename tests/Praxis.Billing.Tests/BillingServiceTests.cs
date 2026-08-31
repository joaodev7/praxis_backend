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
    public async Task CreateCheckoutAsync_Professional_Monthly_ShouldCreateCustomer_CreateCheckout_AndReturnCheckoutUrl()
    {
        // Arrange
        const string mockCustomerId = "cus_mock_12345";
        const string mockCheckoutId = "plk_mock_67890";
        const string mockCheckoutUrl = "https://sandbox.asaas.com/c/plk_mock_67890";

        _paymentGatewayMock.Setup(g => g.GetOrCreateCustomerAsync(It.IsAny<PaymentCustomer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayCustomerResult
            {
                ProviderCustomerId = mockCustomerId,
                Success = true
            });

        _paymentGatewayMock.Setup(g => g.CreateCheckoutAsync(It.IsAny<CreateGatewayCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayCheckoutResult
            {
                ProviderCheckoutId = mockCheckoutId,
                CheckoutUrl = mockCheckoutUrl,
                Success = true
            });

        var request = new CheckoutRequestDto
        {
            PlanCode = "professional",
            BillingCycle = BillingCycle.Monthly
        };

        // Act
        var result = await _sut.CreateCheckoutAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.CheckoutUrl.Should().Be(mockCheckoutUrl);
        result.ProviderCheckoutId.Should().Be(mockCheckoutId);
        result.Amount.Should().Be(299.00m);
        result.Status.Should().Be("pending");

        // Verify DB persistence
        var sub = await _context.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == _tenantId);
        sub.Should().NotBeNull();
        sub!.ProviderCustomerId.Should().Be(mockCustomerId);
        sub.ProviderPaymentLinkId.Should().Be(mockCheckoutId);
        sub.ProviderCheckoutUrl.Should().Be(mockCheckoutUrl);
    }

    [Fact]
    public async Task CreateCheckoutAsync_Essential_Annual_ShouldCreateCheckoutWithAnnualPrice()
    {
        // Arrange
        const string mockCustomerId = "cus_mock_annual_123";
        const string mockCheckoutId = "plk_mock_annual_456";
        const string mockCheckoutUrl = "https://sandbox.asaas.com/c/plk_mock_annual_456";

        _paymentGatewayMock.Setup(g => g.GetOrCreateCustomerAsync(It.IsAny<PaymentCustomer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayCustomerResult
            {
                ProviderCustomerId = mockCustomerId,
                Success = true
            });

        _paymentGatewayMock.Setup(g => g.CreateCheckoutAsync(It.IsAny<CreateGatewayCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayCheckoutResult
            {
                ProviderCheckoutId = mockCheckoutId,
                CheckoutUrl = mockCheckoutUrl,
                Success = true
            });

        var request = new CheckoutRequestDto
        {
            PlanCode = "essential",
            BillingCycle = BillingCycle.Annual
        };

        // Act
        var result = await _sut.CreateCheckoutAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Amount.Should().Be(1490.00m); // 149 * 10 = 1490
        result.CheckoutUrl.Should().Be(mockCheckoutUrl);
        _paymentGatewayMock.Verify(g => g.CreateCheckoutAsync(It.Is<CreateGatewayCheckoutRequest>(r => r.Value == 1490.00m && r.BillingCycle == BillingCycle.Annual), It.IsAny<CancellationToken>()), Times.Once);
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

        _paymentGatewayMock.Setup(g => g.CreateCheckoutAsync(It.IsAny<CreateGatewayCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayCheckoutResult
            {
                ProviderCheckoutId = "plk_999",
                CheckoutUrl = "https://sandbox.asaas.com/c/plk_999",
                Success = true
            });

        var request = new CheckoutRequestDto
        {
            PlanCode = "essential",
            BillingCycle = BillingCycle.Monthly
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
