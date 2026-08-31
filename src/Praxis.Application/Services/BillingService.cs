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
            .ToListAsync(ct);

        return plans
            .OrderBy(p => p.MonthlyPrice)
            .Select(p => new PlanDto
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

        var planCodeLower = request.PlanCode.ToLower();
        var plan = await _context.Plans
            .FirstOrDefaultAsync(p => p.Code.ToLower() == planCodeLower && p.IsActive, ct)
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

        // 2. Determine price
        decimal amount = subscription?.CustomPrice ?? (request.BillingCycle == BillingCycle.Annual ? plan.AnnualPrice : plan.MonthlyPrice);

        // 3. Determine Success URL
        var successUrl = !string.IsNullOrWhiteSpace(request.SuccessUrl)
            ? request.SuccessUrl
            : "https://praxis-frontend.joaodbv.workers.dev/billing/success";

        // 4. Create Hosted Checkout in Asaas
        var checkoutResult = await _paymentGateway.CreateCheckoutAsync(new CreateGatewayCheckoutRequest
        {
            CustomerId = customerResult.ProviderCustomerId,
            PlanName = plan.Name,
            PlanDescription = $"Assinatura Plano PRAXIS {plan.Name} ({(request.BillingCycle == BillingCycle.Annual ? "Anual" : "Mensal")})",
            Value = amount,
            BillingCycle = request.BillingCycle,
            SuccessUrl = successUrl,
            CancelUrl = request.CancelUrl,
            ExternalReference = tenant.Id.ToString()
        }, ct);

        if (!checkoutResult.Success || string.IsNullOrEmpty(checkoutResult.CheckoutUrl))
        {
            throw new InvalidOperationException(checkoutResult.ErrorMessage ?? "Falha ao gerar checkout no gateway de pagamento.");
        }

        // 5. Update or Create local Subscription
        if (subscription == null)
        {
            subscription = new Subscription
            {
                TenantId = tenantId,
                PlanId = plan.Id,
                Status = SubscriptionStatus.Trial, // Remains Trial until payment confirmation via webhook
                BillingCycle = request.BillingCycle,
                StartedAt = DateTime.UtcNow,
                TrialEndsAt = DateTime.UtcNow.AddDays(14),
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = request.BillingCycle == BillingCycle.Annual ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddMonths(1),
                PaymentProvider = "Asaas",
                ProviderCustomerId = customerResult.ProviderCustomerId,
                ProviderPaymentLinkId = checkoutResult.ProviderCheckoutId,
                ProviderCheckoutUrl = checkoutResult.CheckoutUrl
            };
            _context.Subscriptions.Add(subscription);
        }
        else
        {
            subscription.PlanId = plan.Id;
            subscription.BillingCycle = request.BillingCycle;
            subscription.ProviderCustomerId = customerResult.ProviderCustomerId;
            subscription.ProviderPaymentLinkId = checkoutResult.ProviderCheckoutId;
            subscription.ProviderCheckoutUrl = checkoutResult.CheckoutUrl;
            subscription.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);

        return new CheckoutResponseDto
        {
            SubscriptionId = subscription.Id,
            ProviderCheckoutId = checkoutResult.ProviderCheckoutId,
            CheckoutUrl = checkoutResult.CheckoutUrl,
            Status = "pending",
            Amount = amount,
            BillingCycle = request.BillingCycle,
            InvoiceUrl = checkoutResult.CheckoutUrl,
            Message = "Checkout gerado com sucesso. Redirecionando para o pagamento seguro..."
        };
    }

    public async Task<SubscriptionInfoDto> UpgradePlanAsync(UpgradePlanRequestDto request, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var newPlanCodeLower = request.NewPlanCode.ToLower();
        var newPlan = await _context.Plans
            .FirstOrDefaultAsync(p => p.Code.ToLower() == newPlanCodeLower && p.IsActive, ct)
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

        var targetPlanCodeLower = request.NewPlanCode.ToLower();
        var targetPlan = await _context.Plans
            .FirstOrDefaultAsync(p => p.Code.ToLower() == targetPlanCodeLower && p.IsActive, ct)
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
