using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Praxis.Application.Interfaces;
using Praxis.Infrastructure.Data;
using Praxis.Infrastructure.Services;
using System.Text;

namespace Praxis.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var rawConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DATABASE_URL"]
            ?? configuration["DefaultConnection"]
            ?? configuration["ConnectionStrings:DefaultConnection"];

        var connectionString = FormatPostgresConnectionString(rawConnectionString);

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
            }
            else
            {
                // Fallback to SQLite or InMemory if PostgreSQL connection string is not provided yet
                options.UseSqlite("Data Source=praxis.db");
            }
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // JWT Authentication configuration
        var secret = configuration["JWT:KEY"] ?? configuration["Jwt:Secret"] ?? configuration["Jwt:Key"] ?? "PraxisDevelopmentSecretKeyChangeThis123456789";
        var issuer = configuration["JWT:ISSUER"] ?? configuration["Jwt:Issuer"] ?? "Praxis";
        var audience = configuration["JWT:AUDIENCE"] ?? configuration["Jwt:Audience"] ?? "Praxis";
        var key = Encoding.UTF8.GetBytes(secret);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPdfReportService, PdfReportService>();
        services.AddSingleton<IFileStorageService, FileStorageService>();

        // Asaas Payment Provider Configuration
        services.Configure<Praxis.Infrastructure.Billing.PaymentProviders.Asaas.AsaasOptions>(options =>
        {
            configuration.GetSection(Praxis.Infrastructure.Billing.PaymentProviders.Asaas.AsaasOptions.SectionName).Bind(options);

            // Override with environment variables if present
            var envApiKey = configuration["ASAAS_API_KEY"] ?? configuration["ASAAS:API_KEY"];
            if (!string.IsNullOrWhiteSpace(envApiKey)) options.ApiKey = envApiKey;

            var envWebhookToken = configuration["ASAAS_WEBHOOK_TOKEN"] ?? configuration["ASAAS:WEBHOOK_TOKEN"];
            if (!string.IsNullOrWhiteSpace(envWebhookToken)) options.WebhookToken = envWebhookToken;

            var envEnvironment = configuration["ASAAS_ENVIRONMENT"] ?? configuration["ASAAS:ENVIRONMENT"];
            if (!string.IsNullOrWhiteSpace(envEnvironment)) options.Environment = envEnvironment;
        });

        services.AddHttpClient<Praxis.Infrastructure.Billing.PaymentProviders.Asaas.AsaasHttpClient>(client =>
        {
            client.DefaultRequestHeaders.Remove("User-Agent");
            client.DefaultRequestHeaders.Add("User-Agent", "PRAXIS-Nutri-Platform/1.0");
        });
        services.AddScoped<Praxis.Application.Interfaces.IPaymentGateway, Praxis.Infrastructure.Billing.PaymentProviders.Asaas.AsaasPaymentGateway>();
        services.AddScoped<Praxis.Application.Interfaces.IAsaasWebhookService, Praxis.Infrastructure.Billing.PaymentProviders.Asaas.AsaasWebhookService>();
        services.AddHostedService<Praxis.Infrastructure.Billing.BackgroundJobs.BillingBackgroundService>();

        return services;
    }

    private static string? FormatPostgresConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        connectionString = connectionString.Trim().Trim('"').Trim('\'');

        if (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(connectionString);
            var userInfo = uri.UserInfo.Split(':');
            var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');

            var builder = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Database = database,
                Username = username,
                Password = password,
                SslMode = Npgsql.SslMode.Require,
                Timeout = 30,
                CommandTimeout = 30,
                Pooling = true
            };

            return builder.ConnectionString;
        }

        return connectionString;
    }
}
