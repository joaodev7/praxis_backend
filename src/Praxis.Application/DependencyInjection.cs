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

        return services;
    }
}
