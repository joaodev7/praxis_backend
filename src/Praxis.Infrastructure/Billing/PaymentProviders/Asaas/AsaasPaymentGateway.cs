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
            var cleanCpfCnpj = CleanDigits(customer.CpfCnpj);
            var cleanPhone = CleanDigits(customer.Phone);
            var cleanPostalCode = CleanDigits(customer.PostalCode);

            // 1. Search if customer already exists by CPF/CNPJ or email
            var searchParam = !string.IsNullOrWhiteSpace(cleanCpfCnpj) 
                ? $"cpfCnpj={cleanCpfCnpj}" 
                : $"email={Uri.EscapeDataString(customer.Email)}";

            var (searchResponse, _) = await _client.GetWithResultAsync<AsaasListResponse<AsaasCustomerResponse>>($"customers?{searchParam}", ct);

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
                cpfCnpj = cleanCpfCnpj,
                email = customer.Email,
                phone = cleanPhone,
                mobilePhone = cleanPhone,
                postalCode = cleanPostalCode,
                address = customer.Address,
                addressNumber = customer.AddressNumber,
                externalReference = customer.ExternalReference
            };

            var (createResponse, createError) = await _client.PostWithResultAsync<object, AsaasCustomerResponse>("customers", request, ct);
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
                ErrorMessage = createError ?? "Não foi possível criar o cliente no Asaas."
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
                        number = CleanDigits(request.CreditCard.Number),
                        expiryMonth = request.CreditCard.ExpiryMonth,
                        expiryYear = request.CreditCard.ExpiryYear,
                        ccv = request.CreditCard.Ccv
                    },
                    creditCardHolderInfo = request.CreditCardHolderInfo != null ? new
                    {
                        name = request.CreditCardHolderInfo.Name,
                        email = request.CreditCardHolderInfo.Email,
                        cpfCnpj = CleanDigits(request.CreditCardHolderInfo.CpfCnpj),
                        postalCode = CleanDigits(request.CreditCardHolderInfo.PostalCode),
                        addressNumber = request.CreditCardHolderInfo.AddressNumber,
                        phone = CleanDigits(request.CreditCardHolderInfo.Phone)
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

            var (response, subError) = await _client.PostWithResultAsync<object, AsaasSubscriptionResponse>("subscriptions", subPayload, ct);
            if (response == null || string.IsNullOrEmpty(response.Id))
            {
                return new GatewaySubscriptionResult
                {
                    Success = false,
                    ErrorMessage = subError ?? "Falha na criação da assinatura no gateway de pagamento."
                };
            }

            // Retrieve the first generated payment of this subscription
            var (paymentsResponse, _) = await _client.GetWithResultAsync<AsaasListResponse<AsaasPaymentResponse>>($"subscriptions/{response.Id}/payments", ct);
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

            var (response, updateError) = await _client.PostWithResultAsync<object, AsaasSubscriptionResponse>($"subscriptions/{request.ProviderSubscriptionId}", updatePayload, ct);
            if (response == null || string.IsNullOrEmpty(response.Id))
            {
                return new GatewaySubscriptionResult 
                { 
                    Success = false, 
                    ErrorMessage = updateError ?? "Erro ao atualizar assinatura no Asaas." 
                };
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
        try
        {
            var (response, _) = await _client.GetWithResultAsync<AsaasPaymentResponse>($"payments/{providerPaymentId}", ct);
            if (response != null && !string.IsNullOrEmpty(response.Id))
            {
                return new GatewayPaymentResult
                {
                    ProviderPaymentId = response.Id,
                    Status = AsaasStatusMapper.MapPaymentStatus(response.Status),
                    InvoiceUrl = response.InvoiceUrl ?? response.BankSlipUrl,
                    Success = true
                };
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment {PaymentId} in Asaas", providerPaymentId);
            return null;
        }
    }

    public async Task<GatewayPixQrCodeResult?> GetPixQrCodeAsync(string providerPaymentId, CancellationToken ct = default)
    {
        try
        {
            var (response, _) = await _client.GetWithResultAsync<AsaasPixQrCodeResponse>($"payments/{providerPaymentId}/pixQrCode", ct);
            if (response != null && response.Success)
            {
                return new GatewayPixQrCodeResult
                {
                    EncodedImage = response.EncodedImage,
                    Payload = response.Payload,
                    ExpirationDate = response.ExpirationDate,
                    Success = true
                };
            }

            return new GatewayPixQrCodeResult
            {
                Success = false,
                ErrorMessage = "Não foi possível obter o QR Code PIX para o pagamento informado."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PIX QR Code for payment {PaymentId}", providerPaymentId);
            return new GatewayPixQrCodeResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static string? CleanDigits(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private class AsaasListResponse<T>
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("hasMore")]
        public bool HasMore { get; set; }

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        [JsonPropertyName("data")]
        public List<T> Data { get; set; } = new();
    }

    private class AsaasCustomerResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("cpfCnpj")]
        public string? CpfCnpj { get; set; }
    }

    private class AsaasSubscriptionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("customer")]
        public string Customer { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("nextDueDate")]
        public DateTime NextDueDate { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    private class AsaasPaymentResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("invoiceUrl")]
        public string? InvoiceUrl { get; set; }

        [JsonPropertyName("bankSlipUrl")]
        public string? BankSlipUrl { get; set; }
    }

    private class AsaasPixQrCodeResponse
    {
        [JsonPropertyName("encodedImage")]
        public string? EncodedImage { get; set; }

        [JsonPropertyName("payload")]
        public string? Payload { get; set; }

        [JsonPropertyName("expirationDate")]
        public DateTime? ExpirationDate { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }
}
