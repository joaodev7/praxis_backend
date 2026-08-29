using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs.Billing;

public class PaymentCustomer
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CpfCnpj { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? PostalCode { get; set; }
    public string? Address { get; set; }
    public string? AddressNumber { get; set; }
    public string? ExternalReference { get; set; }
}

public class GatewayCustomerResult
{
    public string ProviderCustomerId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CreateGatewaySubscriptionRequest
{
    public string ProviderCustomerId { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime NextDueDate { get; set; }
    public BillingCycle BillingCycle { get; set; }
    public PaymentMethodType PaymentMethod { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ExternalReference { get; set; } = string.Empty;
    
    // Credit card specific
    public CreditCardDataDto? CreditCard { get; set; }
    public CreditCardHolderInfoDto? CreditCardHolderInfo { get; set; }
}

public class GatewaySubscriptionResult
{
    public string ProviderSubscriptionId { get; set; } = string.Empty;
    public string? ProviderPaymentId { get; set; }
    public PaymentStatus Status { get; set; }
    public decimal Value { get; set; }
    public DateTime? NextDueDate { get; set; }
    public string? InvoiceUrl { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ChangeGatewaySubscriptionRequest
{
    public string ProviderSubscriptionId { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public BillingCycle BillingCycle { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class GatewayPaymentResult
{
    public string ProviderPaymentId { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public decimal Value { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? InvoiceUrl { get; set; }
    public string? BankSlipUrl { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class GatewayPixQrCodeResult
{
    public string? EncodedImage { get; set; } // Base64 or Image URL
    public string? Payload { get; set; }      // Copia e Cola code
    public DateTime? ExpirationDate { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
