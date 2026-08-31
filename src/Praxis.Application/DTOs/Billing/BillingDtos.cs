using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs.Billing;

public class PlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public int MaxNutritionists { get; set; }
    public int MaxClientCompanies { get; set; }
    public int MaxStorageMb { get; set; }
    public List<string> Features { get; set; } = new();
}

public class SubscriptionInfoDto
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; }
    public string StatusDescription { get; set; } = string.Empty;
    public BillingCycle BillingCycle { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public int? DaysRemainingInTrial { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? GracePeriodEndsAt { get; set; }
    public bool CancelledAtPeriodEnd { get; set; }
    public decimal CurrentPrice { get; set; }
    
    // Limits and Current Usage
    public int CurrentNutritionistsCount { get; set; }
    public int MaxNutritionists { get; set; }
    public int CurrentClientCompaniesCount { get; set; }
    public int MaxClientCompanies { get; set; }

    // Features
    public List<string> EnabledFeatures { get; set; } = new();
    public bool HasAccess { get; set; }
}

public class CheckoutRequestDto
{
    public string PlanCode { get; set; } = "professional"; // "essential", "professional"
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public string? SuccessUrl { get; set; }
    public string? CancelUrl { get; set; }

    // Optional fields for backward compatibility
    public PaymentMethodType? PaymentMethod { get; set; }
    public CreditCardHolderInfoDto? CreditCardHolderInfo { get; set; }
    public CreditCardDataDto? CreditCard { get; set; }
}

public class CreditCardDataDto
{
    public string HolderName { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string ExpiryMonth { get; set; } = string.Empty;
    public string ExpiryYear { get; set; } = string.Empty;
    public string Ccv { get; set; } = string.Empty;
}

public class CreditCardHolderInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CpfCnpj { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string AddressNumber { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class CheckoutResponseDto
{
    public Guid? SubscriptionId { get; set; }
    public string ProviderCheckoutId { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public decimal Amount { get; set; }
    public BillingCycle BillingCycle { get; set; }
    public string Message { get; set; } = string.Empty;

    // Optional legacy fields for backward compatibility
    public Guid? PaymentId { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public string? InvoiceUrl { get; set; }
    public PixPaymentDataDto? Pix { get; set; }
}

public class PixPaymentDataDto
{
    public string? QrCodeUrl { get; set; }
    public string? CopyPasteCode { get; set; }
    public DateTime? ExpirationDate { get; set; }
}

public class UpgradePlanRequestDto
{
    public string NewPlanCode { get; set; } = "professional";
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
}

public class DowngradePlanRequestDto
{
    public string NewPlanCode { get; set; } = "essential";
}

public class PaymentHistoryDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public string StatusDescription { get; set; } = string.Empty;
    public PaymentMethodType PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? InvoiceUrl { get; set; }
}
