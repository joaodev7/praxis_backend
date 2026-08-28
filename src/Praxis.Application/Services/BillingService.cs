using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs.Billing;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Application.Services;

public class BillingService : IBillingService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IEntitlementService _entitlementService;

    public BillingService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPaymentGateway paymentGateway,
        IEntitlementService entitlementService)
    {
        _context = context;
        _currentUser = currentUser;
        _paymentGateway = paymentGateway;
        _entitlementService = entitlementService;
    }

    public async Task<List<PlanDto>> GetPublicPlansAsync(CancellationToken ct = default)
    {
        var plans = await _context.Plans
            .AsNoTracking()
            .Include(p => p.Features)
            .Where(p => p.IsActive)
            .OrderBy(p => p.MonthlyPrice)
            .ToListAsync(ct);

        return plans.Select(p => new PlanDto
        {
            Id = p.Id,
            Name = p.Name,
            Code = p.Code,
            Description = p.Description,
            MonthlyPrice = p.MonthlyPrice,
            AnnualPrice = p.AnnualPrice,
            MaxNutritionists = p.MaxNutritionists,
            MaxClientCompanies = p.MaxClientCompanies,
            MaxStorageMb = p.MaxStorageMb,
            Features = p.Features.Where(f => f.IsEnabled).Select(f => f.FeatureCode).ToList()
        }).ToList();
    }

    public async Task<SubscriptionInfoDto> GetSubscriptionAsync(CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");
        return await _entitlementService.GetCurrentSubscriptionAsync(tenantId, ct);
    }

    public async Task<CheckoutResponseDto> CreateCheckoutAsync(CheckoutRequestDto request, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var plan = await _context.Plans
            .FirstOrDefaultAsync(p => p.Code.Equals(request.PlanCode, StringComparison.OrdinalIgnoreCase) && p.IsActive, ct)
            ?? throw new KeyNotFoundException($"Plano '{request.PlanCode}' não encontrado ou inativo.");

        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Empresa não encontrada.");

        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        // 1. Ensure Asaas Customer
        var customerResult = await _paymentGateway.GetOrCreateCustomerAsync(new PaymentCustomer
        {
            Name = !string.IsNullOrWhiteSpace(tenant.LegalName) ? tenant.LegalName : tenant.Name,
            Email = tenant.Email,
            CpfCnpj = tenant.Cnpj,
            Phone = tenant.Phone,
            ExternalReference = tenant.Id.ToString()
        }, ct);

        if (!customerResult.Success || string.IsNullOrEmpty(customerResult.ProviderCustomerId))
        {
            throw new InvalidOperationException(customerResult.ErrorMessage ?? "Erro ao registrar cliente no gateway de pagamento.");
        }

        // 2. Determine price and due date
        decimal amount = subscription?.CustomPrice ?? (request.BillingCycle == BillingCycle.Annual ? plan.AnnualPrice : plan.MonthlyPrice);
        var dueDate = DateTime.UtcNow.AddDays(3);

        // 3. Create Subscription in Asaas
        var gatewaySubResult = await _paymentGateway.CreateSubscriptionAsync(new CreateGatewaySubscriptionRequest
        {
            ProviderCustomerId = customerResult.ProviderCustomerId,
            Value = amount,
            NextDueDate = dueDate,
            BillingCycle = request.BillingCycle,
            PaymentMethod = request.PaymentMethod,
            Description = $"PRAXIS {plan.Name} ({request.BillingCycle})",
            ExternalReference = tenant.Id.ToString(),
            CreditCard = request.CreditCard,
            CreditCardHolderInfo = request.CreditCardHolderInfo
        }, ct);

        if (!gatewaySubResult.Success)
        {
            throw new InvalidOperationException(gatewaySubResult.ErrorMessage ?? "Falha ao gerar cobrança no gateway de pagamento.");
        }

        // 4. Update or Create local Subscription
        if (subscription == null)
        {
            subscription = new Subscription
            {
                TenantId = tenantId,
                PlanId = plan.Id,
                Status = SubscriptionStatus.Trial, // Remains Trial or becomes Active upon webhook confirmation
                BillingCycle = request.BillingCycle,
                StartedAt = DateTime.UtcNow,
                TrialEndsAt = DateTime.UtcNow.AddDays(14),
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = request.BillingCycle == BillingCycle.Annual ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddMonths(1),
                PaymentProvider = "Asaas",
                ProviderCustomerId = customerResult.ProviderCustomerId,
                ProviderSubscriptionId = gatewaySubResult.ProviderSubscriptionId
            };
            _context.Subscriptions.Add(subscription);
        }
        else
        {
            subscription.PlanId = plan.Id;
            subscription.BillingCycle = request.BillingCycle;
            subscription.ProviderCustomerId = customerResult.ProviderCustomerId;
            subscription.ProviderSubscriptionId = gatewaySubResult.ProviderSubscriptionId;
            subscription.UpdatedAt = DateTime.UtcNow;
        }

        // 5. Create Payment record
        var payment = new Payment
        {
            TenantId = tenantId,
            Subscription = subscription,
            ProviderPaymentId = gatewaySubResult.ProviderPaymentId,
            Amount = amount,
            Status = gatewaySubResult.Status,
            DueDate = dueDate,
            PaymentMethod = request.PaymentMethod,
            Provider = "Asaas",
            InvoiceUrl = gatewaySubResult.InvoiceUrl,
            CardBrand = request.CreditCard != null ? "Cartão de Crédito" : null,
            CardLastFour = request.CreditCard?.Number?.Length >= 4 ? request.CreditCard.Number[^4..] : null
        };

        // 6. If Pix, fetch QR code and copy/paste string
        PixPaymentDataDto? pixData = null;
        if (request.PaymentMethod == PaymentMethodType.Pix && !string.IsNullOrEmpty(gatewaySubResult.ProviderPaymentId))
        {
            var pixResult = await _paymentGateway.GetPixQrCodeAsync(gatewaySubResult.ProviderPaymentId, ct);
            if (pixResult != null && pixResult.Success)
            {
                payment.PixQrCodeUrl = pixResult.EncodedImage;
                payment.PixCopyPasteCode = pixResult.Payload;

                pixData = new PixPaymentDataDto
                {
                    QrCodeUrl = pixResult.EncodedImage,
                    CopyPasteCode = pixResult.Payload,
                    ExpirationDate = pixResult.ExpirationDate ?? dueDate
                };
            }
        }

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(ct);

        return new CheckoutResponseDto
        {
            PaymentId = payment.Id,
            Status = payment.Status,
            Amount = payment.Amount,
            PaymentMethod = payment.PaymentMethod,
            DueDate = payment.DueDate,
            InvoiceUrl = payment.InvoiceUrl,
            Pix = pixData,
            Message = payment.PaymentMethod == PaymentMethodType.CreditCard && payment.Status == PaymentStatus.Confirmed
                ? "Pagamento via cartão aprovado com sucesso! Sua assinatura está ativa."
                : "Cobrança gerada com sucesso. Realize o pagamento para ativar sua assinatura."
        };
    }

    public async Task<SubscriptionInfoDto> UpgradePlanAsync(UpgradePlanRequestDto request, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var newPlan = await _context.Plans
            .FirstOrDefaultAsync(p => p.Code.Equals(request.NewPlanCode, StringComparison.OrdinalIgnoreCase) && p.IsActive, ct)
            ?? throw new KeyNotFoundException($"Plano '{request.NewPlanCode}' não encontrado.");

        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("Assinatura não encontrada.");

        decimal newAmount = subscription.CustomPrice ?? (request.BillingCycle == BillingCycle.Annual ? newPlan.AnnualPrice : newPlan.MonthlyPrice);

        if (!string.IsNullOrEmpty(subscription.ProviderSubscriptionId))
        {
            await _paymentGateway.ChangeSubscriptionAsync(new ChangeGatewaySubscriptionRequest
            {
                ProviderSubscriptionId = subscription.ProviderSubscriptionId,
                Value = newAmount,
                BillingCycle = request.BillingCycle,
                Description = $"PRAXIS {newPlan.Name} ({request.BillingCycle})"
            }, ct);
        }

        subscription.PlanId = newPlan.Id;
        subscription.BillingCycle = request.BillingCycle;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return await _entitlementService.GetCurrentSubscriptionAsync(tenantId, ct);
    }

    public async Task<SubscriptionInfoDto> DowngradePlanAsync(DowngradePlanRequestDto request, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var targetPlan = await _context.Plans
            .FirstOrDefaultAsync(p => p.Code.Equals(request.NewPlanCode, StringComparison.OrdinalIgnoreCase) && p.IsActive, ct)
            ?? throw new KeyNotFoundException($"Plano '{request.NewPlanCode}' não encontrado.");

        // Check active entity limits
        int currentNutritionists = await _context.Nutritionists.CountAsync(n => n.TenantId == tenantId, ct);
        if (currentNutritionists > targetPlan.MaxNutritionists)
        {
            throw new InvalidOperationException($"Não é possível alterar para o plano {targetPlan.Name}. Sua empresa possui {currentNutritionists} nutricionistas cadastrados e o limite do novo plano é {targetPlan.MaxNutritionists}.");
        }

        int currentClients = await _context.ClientCompanies.CountAsync(c => c.TenantId == tenantId, ct);
        if (currentClients > targetPlan.MaxClientCompanies)
        {
            throw new InvalidOperationException($"Não é possível alterar para o plano {targetPlan.Name}. Sua empresa possui {currentClients} clientes cadastrados e o limite do novo plano é {targetPlan.MaxClientCompanies}.");
        }

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("Assinatura não encontrada.");

        decimal newAmount = subscription.BillingCycle == BillingCycle.Annual ? targetPlan.AnnualPrice : targetPlan.MonthlyPrice;

        if (!string.IsNullOrEmpty(subscription.ProviderSubscriptionId))
        {
            await _paymentGateway.ChangeSubscriptionAsync(new ChangeGatewaySubscriptionRequest
            {
                ProviderSubscriptionId = subscription.ProviderSubscriptionId,
                Value = newAmount,
                BillingCycle = subscription.BillingCycle,
                Description = $"PRAXIS {targetPlan.Name} ({subscription.BillingCycle})"
            }, ct);
        }

        subscription.PlanId = targetPlan.Id;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return await _entitlementService.GetCurrentSubscriptionAsync(tenantId, ct);
    }

    public async Task<SubscriptionInfoDto> CancelSubscriptionAsync(CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("Assinatura não encontrada.");

        if (!string.IsNullOrEmpty(subscription.ProviderSubscriptionId))
        {
            await _paymentGateway.CancelSubscriptionAsync(subscription.ProviderSubscriptionId, ct);
        }

        subscription.EndsAtPeriodEnd = true;
        subscription.CancelledAt = DateTime.UtcNow;
        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return await _entitlementService.GetCurrentSubscriptionAsync(tenantId, ct);
    }

    public async Task<SubscriptionInfoDto> ReactivateSubscriptionAsync(CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("Assinatura não encontrada.");

        subscription.EndsAtPeriodEnd = false;
        subscription.CancelledAt = null;
        subscription.Status = SubscriptionStatus.Active;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return await _entitlementService.GetCurrentSubscriptionAsync(tenantId, ct);
    }

    public async Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var payments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return payments.Select(p => new PaymentHistoryDto
        {
            Id = p.Id,
            Amount = p.Amount,
            Status = p.Status,
            StatusDescription = p.Status switch
            {
                PaymentStatus.Pending => "Pendente",
                PaymentStatus.Confirmed => "Confirmado / Pago",
                PaymentStatus.Overdue => "Vencido",
                PaymentStatus.Failed => "Falha",
                PaymentStatus.Refunded => "Estornado",
                PaymentStatus.Cancelled => "Cancelado",
                _ => "Pendente"
            },
            PaymentMethod = p.PaymentMethod,
            CreatedAt = p.CreatedAt,
            DueDate = p.DueDate,
            PaidAt = p.PaidAt,
            InvoiceUrl = p.InvoiceUrl
        }).ToList();
    }
}
