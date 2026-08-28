using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs.Billing;
using Praxis.Application.Interfaces;
using Praxis.Domain.Enums;

namespace Praxis.Application.Services;

public class EntitlementService : IEntitlementService
{
    private readonly IApplicationDbContext _context;

    public EntitlementService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasActiveAccessAsync(Guid tenantId, CancellationToken ct = default)
    {
        var subscription = await _context.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (subscription == null)
            return true; // Graceful fallback if no subscription configured yet

        var now = DateTime.UtcNow;

        return subscription.Status switch
        {
            SubscriptionStatus.Trial => subscription.TrialEndsAt == null || subscription.TrialEndsAt.Value > now,
            SubscriptionStatus.Active => true,
            SubscriptionStatus.PastDue => subscription.GracePeriodEndsAt == null || subscription.GracePeriodEndsAt.Value > now,
            SubscriptionStatus.Cancelled => subscription.CurrentPeriodEnd == null || subscription.CurrentPeriodEnd.Value > now,
            SubscriptionStatus.Suspended => false,
            SubscriptionStatus.Expired => false,
            _ => false
        };
    }

    public async Task<bool> HasFeatureAsync(Guid tenantId, string featureCode, CancellationToken ct = default)
    {
        var hasAccess = await HasActiveAccessAsync(tenantId, ct);
        if (!hasAccess)
            return false;

        var subscription = await _context.Subscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
                .ThenInclude(p => p.Features)
            .Include(s => s.Overrides)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (subscription == null)
            return true; // Fallback

        // Check specific override first
        var featureOverride = subscription.Overrides.FirstOrDefault(o => o.FeatureCode.Equals(featureCode, StringComparison.OrdinalIgnoreCase));
        if (featureOverride != null)
            return featureOverride.IsEnabled;

        // Check plan features
        var planFeature = subscription.Plan.Features.FirstOrDefault(f => f.FeatureCode.Equals(featureCode, StringComparison.OrdinalIgnoreCase));
        return planFeature?.IsEnabled ?? false;
    }

    public async Task ValidateLimitAsync(Guid tenantId, string limitCode, int requestedQuantity = 1, CancellationToken ct = default)
    {
        var hasAccess = await HasActiveAccessAsync(tenantId, ct);
        if (!hasAccess)
            throw new InvalidOperationException("O período de acesso ou assinatura da sua empresa está suspenso ou expirado. Regularize sua assinatura para continuar cadastrando.");

        var subscription = await _context.Subscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Include(s => s.Overrides)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (subscription == null)
            return;

        if (limitCode.Equals("max_nutritionists", StringComparison.OrdinalIgnoreCase))
        {
            int maxAllowed = subscription.Plan.MaxNutritionists;
            var overrideVal = subscription.Overrides.FirstOrDefault(o => o.FeatureCode.Equals("max_nutritionists", StringComparison.OrdinalIgnoreCase));
            if (overrideVal != null && int.TryParse(overrideVal.CustomValue, out int customMax))
            {
                maxAllowed = customMax;
            }

            int currentCount = await _context.Nutritionists.CountAsync(n => n.TenantId == tenantId, ct);
            if (currentCount + requestedQuantity > maxAllowed)
            {
                throw new InvalidOperationException($"Limite de nutricionistas atingido para o plano atual ({currentCount}/{maxAllowed}). Faça upgrade para o plano Profissional ou Enterprise para adicionar mais profissionais.");
            }
        }
        else if (limitCode.Equals("max_client_companies", StringComparison.OrdinalIgnoreCase))
        {
            int maxAllowed = subscription.Plan.MaxClientCompanies;
            var overrideVal = subscription.Overrides.FirstOrDefault(o => o.FeatureCode.Equals("max_client_companies", StringComparison.OrdinalIgnoreCase));
            if (overrideVal != null && int.TryParse(overrideVal.CustomValue, out int customMax))
            {
                maxAllowed = customMax;
            }

            int currentCount = await _context.ClientCompanies.CountAsync(c => c.TenantId == tenantId, ct);
            if (currentCount + requestedQuantity > maxAllowed)
            {
                throw new InvalidOperationException($"Limite de empresas clientes atingido para o plano atual ({currentCount}/{maxAllowed}). Faça upgrade para o plano Profissional ou Enterprise para cadastrar novos clientes.");
            }
        }
    }

    public async Task<SubscriptionInfoDto> GetCurrentSubscriptionAsync(Guid tenantId, CancellationToken ct = default)
    {
        var subscription = await _context.Subscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
                .ThenInclude(p => p.Features)
            .Include(s => s.Overrides)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (subscription == null)
        {
            // If none, return default trial placeholder
            return new SubscriptionInfoDto
            {
                PlanName = "Profissional (Trial)",
                PlanCode = "professional",
                Status = SubscriptionStatus.Trial,
                StatusDescription = "Período de Testes",
                BillingCycle = BillingCycle.Monthly,
                StartedAt = DateTime.UtcNow,
                DaysRemainingInTrial = 14,
                CurrentPrice = 299.00m,
                MaxNutritionists = 10,
                MaxClientCompanies = 50,
                HasAccess = true
            };
        }

        var now = DateTime.UtcNow;
        int? daysRemaining = null;
        if (subscription.Status == SubscriptionStatus.Trial && subscription.TrialEndsAt.HasValue)
        {
            daysRemaining = Math.Max(0, (int)Math.Ceiling((subscription.TrialEndsAt.Value - now).TotalDays));
        }

        int currentNutritionists = await _context.Nutritionists.CountAsync(n => n.TenantId == tenantId, ct);
        int currentClients = await _context.ClientCompanies.CountAsync(c => c.TenantId == tenantId, ct);

        var enabledFeatures = subscription.Plan.Features
            .Where(f => f.IsEnabled)
            .Select(f => f.FeatureCode)
            .ToList();

        // Apply overrides
        foreach (var ov in subscription.Overrides)
        {
            if (ov.IsEnabled && !enabledFeatures.Contains(ov.FeatureCode))
                enabledFeatures.Add(ov.FeatureCode);
            else if (!ov.IsEnabled)
                enabledFeatures.Remove(ov.FeatureCode);
        }

        int maxNutris = subscription.Plan.MaxNutritionists;
        int maxClients = subscription.Plan.MaxClientCompanies;
        var nutrisOv = subscription.Overrides.FirstOrDefault(o => o.FeatureCode.Equals("max_nutritionists", StringComparison.OrdinalIgnoreCase));
        if (nutrisOv != null && int.TryParse(nutrisOv.CustomValue, out int customNutris)) maxNutris = customNutris;

        var clientsOv = subscription.Overrides.FirstOrDefault(o => o.FeatureCode.Equals("max_client_companies", StringComparison.OrdinalIgnoreCase));
        if (clientsOv != null && int.TryParse(clientsOv.CustomValue, out int customClients)) maxClients = customClients;

        bool hasAccess = subscription.Status switch
        {
            SubscriptionStatus.Trial => subscription.TrialEndsAt == null || subscription.TrialEndsAt.Value > now,
            SubscriptionStatus.Active => true,
            SubscriptionStatus.PastDue => subscription.GracePeriodEndsAt == null || subscription.GracePeriodEndsAt.Value > now,
            SubscriptionStatus.Cancelled => subscription.CurrentPeriodEnd == null || subscription.CurrentPeriodEnd.Value > now,
            _ => false
        };

        string statusDesc = subscription.Status switch
        {
            SubscriptionStatus.Trial => $"Trial Ativo ({daysRemaining ?? 0} dias restantes)",
            SubscriptionStatus.Active => "Assinatura Ativa",
            SubscriptionStatus.PastDue => "Pagamento Pendente",
            SubscriptionStatus.Suspended => "Acesso Suspenso por Inadimplência",
            SubscriptionStatus.Cancelled => "Cancelamento Agendado",
            SubscriptionStatus.Expired => "Período de Testes Expirado",
            _ => "Indefinido"
        };

        decimal effectivePrice = subscription.CustomPrice ?? (subscription.BillingCycle == BillingCycle.Annual ? subscription.Plan.AnnualPrice : subscription.Plan.MonthlyPrice);

        return new SubscriptionInfoDto
        {
            Id = subscription.Id,
            PlanName = subscription.Plan.Name,
            PlanCode = subscription.Plan.Code,
            Status = subscription.Status,
            StatusDescription = statusDesc,
            BillingCycle = subscription.BillingCycle,
            StartedAt = subscription.StartedAt,
            TrialEndsAt = subscription.TrialEndsAt,
            DaysRemainingInTrial = daysRemaining,
            CurrentPeriodEnd = subscription.CurrentPeriodEnd,
            GracePeriodEndsAt = subscription.GracePeriodEndsAt,
            CancelledAtPeriodEnd = subscription.EndsAtPeriodEnd,
            CurrentPrice = effectivePrice,
            CurrentNutritionistsCount = currentNutritionists,
            MaxNutritionists = maxNutris,
            CurrentClientCompaniesCount = currentClients,
            MaxClientCompanies = maxClients,
            EnabledFeatures = enabledFeatures,
            HasAccess = hasAccess
        };
    }
}
