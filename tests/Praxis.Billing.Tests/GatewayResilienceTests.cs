using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Praxis.Application.DTOs.Billing;
using Praxis.Infrastructure.Billing.PaymentProviders.Asaas;
using Xunit;

namespace Praxis.Billing.Tests;

public class GatewayResilienceTests
{
    [Fact]
    public async Task GetOrCreateCustomerAsync_WhenAsaasReturns500_ShouldReturnGracefulFailureResult()
    {
        // Arrange: Mock HttpMessageHandler returning 500 Internal Server Error
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent(@"{""errors"":[{""description"":""Erro interno temporário no Asaas""}]}")
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://sandbox.asaas.com/api/v3/")
        };

        var options = Options.Create(new AsaasOptions
        {
            Environment = "Sandbox",
            ApiKey = "test_key",
            WebhookToken = "test_token"
        });

        var httpClientLogger = new Mock<ILogger<AsaasHttpClient>>();
        var asaasHttpClient = new AsaasHttpClient(httpClient, options, httpClientLogger.Object);
        var logger = new Mock<ILogger<AsaasPaymentGateway>>();
        var gateway = new AsaasPaymentGateway(asaasHttpClient, logger.Object);

        // Act
        var result = await gateway.GetOrCreateCustomerAsync(new PaymentCustomer
        {
            Name = "Nutri Teste",
            Email = "nutri@teste.com"
        });

        // Assert: System does not crash or throw unhandled exceptions
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateSubscriptionAsync_WhenNetworkFails_ShouldReturnFailureWithoutCorruptingState()
    {
        // Arrange: Mock HttpMessageHandler throwing HttpRequestException (network timeout / dropped connection)
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection timed out to Asaas Gateway"));

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://sandbox.asaas.com/api/v3/")
        };

        var options = Options.Create(new AsaasOptions
        {
            Environment = "Sandbox",
            ApiKey = "test_key"
        });

        var httpClientLogger = new Mock<ILogger<AsaasHttpClient>>();
        var asaasHttpClient = new AsaasHttpClient(httpClient, options, httpClientLogger.Object);
        var logger = new Mock<ILogger<AsaasPaymentGateway>>();
        var gateway = new AsaasPaymentGateway(asaasHttpClient, logger.Object);

        // Act
        var result = await gateway.CreateSubscriptionAsync(new CreateGatewaySubscriptionRequest
        {
            ProviderCustomerId = "cus_123",
            Value = 299.00m,
            BillingCycle = Domain.Enums.BillingCycle.Monthly,
            PaymentMethod = Domain.Enums.PaymentMethodType.Pix
        });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }
}
