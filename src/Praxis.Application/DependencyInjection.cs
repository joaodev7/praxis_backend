using Microsoft.Extensions.DependencyInjection;
using Praxis.Application.Services;

namespace Praxis.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<NutritionistService>();
        services.AddScoped<ClientService>();
        services.AddScoped<UnitService>();
        services.AddScoped<ArtService>();
        services.AddScoped<ChecklistService>();
        services.AddScoped<VisitService>();
        services.AddScoped<NonConformityService>();
        services.AddScoped<EvidenceService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<Praxis.Application.Interfaces.IEntitlementService, EntitlementService>();
        services.AddScoped<Praxis.Application.Interfaces.IBillingService, BillingService>();
        services.AddScoped<FileService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<Praxis.Application.Interfaces.IActionPlanService, ActionPlanService>();
        services.AddScoped<ActionPlanService>();

        // Etiquetagem e Gestão de Validade
        services.AddScoped<Praxis.Domain.Services.IValidityCalculator, Praxis.Domain.Services.ValidityCalculator>();
        services.AddScoped<Praxis.Application.Interfaces.IProductService, ProductService>();
        services.AddScoped<ProductService>();
        services.AddScoped<Praxis.Application.Interfaces.IValidityRuleService, ValidityRuleService>();
        services.AddScoped<ValidityRuleService>();
        services.AddScoped<Praxis.Application.Interfaces.IFoodLabelService, FoodLabelService>();
        services.AddScoped<FoodLabelService>();

        return services;
    }
}
