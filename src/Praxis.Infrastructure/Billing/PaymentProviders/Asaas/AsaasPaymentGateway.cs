using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Praxis.Application.DTOs.Billing;
using Praxis.Application.Interfaces;
using Praxis.Domain.Enums;

namespace Praxis.Infrastructure.Billing.PaymentProviders.Asaas;

public class AsaasPaymentGateway : IPaymentGateway
{
    private readonly AsaasHttpClient _client;
    private readonly ILogger<AsaasPaymentGateway> _logger;

    public AsaasPaymentGateway(AsaasHttpClient client, ILogger<AsaasPaymentGateway> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<GatewayCustomerResult> GetOrCreateCustomerAsync(PaymentCustomer customer, CancellationToken ct = default)
    {
        try
        {
            // 1. Search if customer already exists by CPF/CNPJ or email
            var searchParam = !string.IsNullOrWhiteSpace(customer.CpfCnpj) ? $"cpfCnpj={customer.CpfCnpj}" : $"email={customer.Email}";
            var searchResponse = await _client.GetAsync<AsaasListResponse<AsaasCustomerResponse>>($"customers?{searchParam}", ct);

            if (searchResponse?.Data != null && searchResponse.Data.Count > 0)
            {
                var existing = searchResponse.Data[0];
                return new GatewayCustomerResult
                {
                    ProviderCustomerId = existing.Id,
                    Success = true
                };
            }

            // 2. Create customer
            var request = new
            {
                name = customer.Name,
                cpfCnpj = customer.CpfCnpj,
                email = customer.Email,
                phone = customer.Phone,
                postalCode = customer.PostalCode,
                address = customer.Address,
                addressNumber = customer.AddressNumber,
                externalReference = customer.ExternalReference
            };

            var createResponse = await _client.PostAsync<object, AsaasCustomerResponse>("customers", request, ct);
            if (createResponse != null && !string.IsNullOrEmpty(createResponse.Id))
            {
                return new GatewayCustomerResult
                {
                    ProviderCustomerId = createResponse.Id,
                    Success = true
                };
            }

            return new GatewayCustomerResult
            {
                Success = false,
                ErrorMessage = "Não foi possível criar o cliente no Asaas."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating or fetching customer in Asaas for {Email}", customer.Email);
            return new GatewayCustomerResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<GatewaySubscriptionResult> CreateSubscriptionAsync(CreateGatewaySubscriptionRequest request, CancellationToken ct = default)
    {
        try
        {
            string billingType = request.PaymentMethod switch
            {
                PaymentMethodType.Pix => "PIX",
                PaymentMethodType.CreditCard => "CREDIT_CARD",
                PaymentMethodType.Boleto => "BOLETO",
                _ => "PIX"
            };

            string cycle = request.BillingCycle == BillingCycle.Annual ? "YEARLY" : "MONTHLY";

            object subPayload;

            if (request.PaymentMethod == PaymentMethodType.CreditCard && request.CreditCard != null)
            {
                subPayload = new
                {
                    customer = request.ProviderCustomerId,
                    billingType,
                    value = request.Value,
                    nextDueDate = request.NextDueDate.ToString("yyyy-MM-dd"),
                    cycle,
                    description = request.Description,
                    externalReference = request.ExternalReference,
                    creditCard = new
                    {
                        holderName = request.CreditCard.HolderName,
                        number = request.CreditCard.Number,
                        expiryMonth = request.CreditCard.ExpiryMonth,
                        expiryYear = request.CreditCard.ExpiryYear,
                        ccv = request.CreditCard.Ccv
                    },
                    creditCardHolderInfo = request.CreditCardHolderInfo != null ? new
                    {
                        name = request.CreditCardHolderInfo.Name,
                        email = request.CreditCardHolderInfo.Email,
                        cpfCnpj = request.CreditCardHolderInfo.CpfCnpj,
                        postalCode = request.CreditCardHolderInfo.PostalCode,
                        addressNumber = request.CreditCardHolderInfo.AddressNumber,
                        phone = request.CreditCardHolderInfo.Phone
                    } : null
                };
            }
            else
            {
                subPayload = new
                {
                    customer = request.ProviderCustomerId,
                    billingType,
                    value = request.Value,
                    nextDueDate = request.NextDueDate.ToString("yyyy-MM-dd"),
                    cycle,
                    description = request.Description,
                    externalReference = request.ExternalReference
                };
            }

            var response = await _client.PostAsync<object, AsaasSubscriptionResponse>("subscriptions", subPayload, ct);
            if (response == null || string.IsNullOrEmpty(response.Id))
            {
                return new GatewaySubscriptionResult
                {
                    Success = false,
                    ErrorMessage = "Falha na criação da assinatura no gateway de pagamento."
                };
            }

            // Retrieve the first generated payment of this subscription
            var paymentsResponse = await _client.GetAsync<AsaasListResponse<AsaasPaymentResponse>>($"subscriptions/{response.Id}/payments", ct);
            var firstPayment = paymentsResponse?.Data?.FirstOrDefault();

            return new GatewaySubscriptionResult
            {
                ProviderSubscriptionId = response.Id,
                ProviderPaymentId = firstPayment?.Id,
                Status = AsaasStatusMapper.MapPaymentStatus(firstPayment?.Status ?? response.Status),
                Value = response.Value,
                NextDueDate = response.NextDueDate,
                InvoiceUrl = firstPayment?.InvoiceUrl ?? firstPayment?.BankSlipUrl,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subscription in Asaas for customer {Customer}", request.ProviderCustomerId);
            return new GatewaySubscriptionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<GatewaySubscriptionResult> ChangeSubscriptionAsync(ChangeGatewaySubscriptionRequest request, CancellationToken ct = default)
    {
        try
        {
            string cycle = request.BillingCycle == BillingCycle.Annual ? "YEARLY" : "MONTHLY";

            var updatePayload = new
            {
                value = request.Value,
                cycle,
                description = request.Description,
                updatePendingPayments = true
            };

            var response = await _client.PostAsync<object, AsaasSubscriptionResponse>($"subscriptions/{request.ProviderSubscriptionId}", updatePayload, ct);
            if (response == null || string.IsNullOrEmpty(response.Id))
            {
                return new GatewaySubscriptionResult { Success = false, ErrorMessage = "Erro ao atualizar assinatura no Asaas." };
            }

            return new GatewaySubscriptionResult
            {
                ProviderSubscriptionId = response.Id,
                Status = AsaasStatusMapper.MapPaymentStatus(response.Status),
                Value = response.Value,
                NextDueDate = response.NextDueDate,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating subscription {SubscriptionId} in Asaas", request.ProviderSubscriptionId);
            return new GatewaySubscriptionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task CancelSubscriptionAsync(string providerSubscriptionId, CancellationToken ct = default)
    {
        try
        {
            await _client.DeleteAsync($"subscriptions/{providerSubscriptionId}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling subscription {SubscriptionId} in Asaas", providerSubscriptionId);
        }
    }

    public async Task<GatewayPaymentResult?> GetPaymentAsync(string providerPaymentId, CancellationToken ct = default)
    {
        var response = await _client.GetAsync<AsaasPaymentResponse>($"payments/{providerPaymentId}", ct);
        if (response == null) return null;

        return new GatewayPaymentResult
        {
            ProviderPaymentId = response.Id,
            Status = AsaasStatusMapper.MapPaymentStatus(response.Status),
            Value = response.Value,
            DueDate = response.DueDate,
            PaymentDate = response.PaymentDate,
            InvoiceUrl = response.InvoiceUrl,
            BankSlipUrl = response.BankSlipUrl
        };
    }

    public async Task<GatewayPixQrCodeResult?> GetPixQrCodeAsync(string providerPaymentId, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetAsync<AsaasPixQrCodeResponse>($"payments/{providerPaymentId}/pixQrCode", ct);
            if (response == null || !response.Success)
            {
                return new GatewayPixQrCodeResult
                {
                    Success = false,
                    ErrorMessage = "Não foi possível obter o QR Code PIX para esta cobrança."
                };
            }

            return new GatewayPixQrCodeResult
            {
                EncodedImage = response.EncodedImage,
                Payload = response.Payload,
                ExpirationDate = response.ExpirationDate,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting PIX QR code for payment {PaymentId}", providerPaymentId);
            return new GatewayPixQrCodeResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}

// Internal Asaas DTOs
internal class AsaasListResponse<T>
{
    public List<T>? Data { get; set; }
}

internal class AsaasCustomerResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

internal class AsaasSubscriptionResponse
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime? NextDueDate { get; set; }
}

internal class AsaasPaymentResponse
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? InvoiceUrl { get; set; }
    public string? BankSlipUrl { get; set; }
}

internal class AsaasPixQrCodeResponse
{
    public bool Success { get; set; } = true;
    public string? EncodedImage { get; set; }
    public string? Payload { get; set; }
    public DateTime? ExpirationDate { get; set; }
}
