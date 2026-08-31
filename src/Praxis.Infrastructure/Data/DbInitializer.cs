using Microsoft.EntityFrameworkCore;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        await EnsureTablesCreatedAsync(context);

        // 1. Seed Plans if not present
        if (!await context.Plans.IgnoreQueryFilters().AnyAsync())
        {
            var planEssential = new Plan
            {
                Name = "PRAXIS Essencial",
                Code = "essential",
                Description = "Ideal para pequenas consultorias e empresas que estão estruturando sua operação.",
                MonthlyPrice = 149.00m,
                AnnualPrice = 1490.00m,
                MaxNutritionists = 3,
                MaxClientCompanies = 10,
                MaxStorageMb = 1000,
                IsActive = true,
                Features = new List<PlanFeature>
                {
                    new() { FeatureCode = "dashboard", IsEnabled = true },
                    new() { FeatureCode = "clients", IsEnabled = true },
                    new() { FeatureCode = "units", IsEnabled = true },
                    new() { FeatureCode = "nutritionists", IsEnabled = true },
                    new() { FeatureCode = "arts", IsEnabled = true },
                    new() { FeatureCode = "visits", IsEnabled = true },
                    new() { FeatureCode = "checklists", IsEnabled = true },
                    new() { FeatureCode = "photos", IsEnabled = true },
                    new() { FeatureCode = "pdf_export", IsEnabled = true }
                }
            };

            var planProfessional = new Plan
            {
                Name = "PRAXIS Profissional",
                Code = "professional",
                Description = "Para consultorias em crescimento que precisam de indicadores e gestão mais completa.",
                MonthlyPrice = 299.00m,
                AnnualPrice = 2990.00m,
                MaxNutritionists = 10,
                MaxClientCompanies = 50,
                MaxStorageMb = 5000,
                IsActive = true,
                Features = new List<PlanFeature>
                {
                    new() { FeatureCode = "dashboard", IsEnabled = true },
                    new() { FeatureCode = "clients", IsEnabled = true },
                    new() { FeatureCode = "units", IsEnabled = true },
                    new() { FeatureCode = "nutritionists", IsEnabled = true },
                    new() { FeatureCode = "arts", IsEnabled = true },
                    new() { FeatureCode = "visits", IsEnabled = true },
                    new() { FeatureCode = "checklists", IsEnabled = true },
                    new() { FeatureCode = "photos", IsEnabled = true },
                    new() { FeatureCode = "pdf_export", IsEnabled = true },
                    new() { FeatureCode = "advanced_analytics", IsEnabled = true },
                    new() { FeatureCode = "period_comparison", IsEnabled = true },
                    new() { FeatureCode = "excel_export", IsEnabled = true },
                    new() { FeatureCode = "custom_reports", IsEnabled = true },
                    new() { FeatureCode = "priority_support", IsEnabled = true }
                }
            };

            var planEnterprise = new Plan
            {
                Name = "PRAXIS Enterprise",
                Code = "enterprise",
                Description = "Operações maiores ou redes com necessidades específicas e limites personalizados.",
                MonthlyPrice = 0.00m,
                AnnualPrice = 0.00m,
                MaxNutritionists = 999,
                MaxClientCompanies = 999,
                MaxStorageMb = 50000,
                IsActive = true,
                Features = new List<PlanFeature>
                {
                    new() { FeatureCode = "dashboard", IsEnabled = true },
                    new() { FeatureCode = "clients", IsEnabled = true },
                    new() { FeatureCode = "units", IsEnabled = true },
                    new() { FeatureCode = "nutritionists", IsEnabled = true },
                    new() { FeatureCode = "arts", IsEnabled = true },
                    new() { FeatureCode = "visits", IsEnabled = true },
                    new() { FeatureCode = "checklists", IsEnabled = true },
                    new() { FeatureCode = "photos", IsEnabled = true },
                    new() { FeatureCode = "pdf_export", IsEnabled = true },
                    new() { FeatureCode = "advanced_analytics", IsEnabled = true },
                    new() { FeatureCode = "period_comparison", IsEnabled = true },
                    new() { FeatureCode = "excel_export", IsEnabled = true },
                    new() { FeatureCode = "custom_reports", IsEnabled = true },
                    new() { FeatureCode = "priority_support", IsEnabled = true },
                    new() { FeatureCode = "dedicated_support", IsEnabled = true },
                    new() { FeatureCode = "custom_integrations", IsEnabled = true }
                }
            };

            context.Plans.AddRange(planEssential, planProfessional, planEnterprise);
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsureTablesCreatedAsync(ApplicationDbContext context)
    {
        try
        {
            if (context.Database.IsNpgsql())
            {
                var sql = @"
CREATE TABLE IF NOT EXISTS ""Plans"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""Name"" text NOT NULL,
    ""Code"" text NOT NULL,
    ""Description"" text NOT NULL,
    ""MonthlyPrice"" numeric NOT NULL,
    ""AnnualPrice"" numeric NOT NULL,
    ""MaxNutritionists"" integer NOT NULL,
    ""MaxClientCompanies"" integer NOT NULL,
    ""MaxStorageMb"" integer NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Plans_Code"" ON ""Plans"" (""Code"");

CREATE TABLE IF NOT EXISTS ""PlanFeatures"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""PlanId"" uuid NOT NULL REFERENCES ""Plans"" (""Id"") ON DELETE CASCADE,
    ""FeatureCode"" text NOT NULL,
    ""IsEnabled"" boolean NOT NULL DEFAULT TRUE,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);

CREATE TABLE IF NOT EXISTS ""Subscriptions"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""TenantId"" uuid NOT NULL REFERENCES ""Tenants"" (""Id"") ON DELETE RESTRICT,
    ""PlanId"" uuid NOT NULL REFERENCES ""Plans"" (""Id"") ON DELETE RESTRICT,
    ""Status"" integer NOT NULL,
    ""BillingCycle"" integer NOT NULL,
    ""StartedAt"" timestamp with time zone NOT NULL,
    ""TrialEndsAt"" timestamp with time zone,
    ""CurrentPeriodStart"" timestamp with time zone,
    ""CurrentPeriodEnd"" timestamp with time zone,
    ""GracePeriodEndsAt"" timestamp with time zone,
    ""CancelledAt"" timestamp with time zone,
    ""EndsAtPeriodEnd"" boolean NOT NULL DEFAULT FALSE,
    ""PaymentProvider"" text NOT NULL DEFAULT 'Asaas',
    ""ProviderCustomerId"" text,
    ""ProviderSubscriptionId"" text,
    ""CustomPrice"" numeric,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);
CREATE INDEX IF NOT EXISTS ""IX_Subscriptions_TenantId"" ON ""Subscriptions"" (""TenantId"");
CREATE INDEX IF NOT EXISTS ""IX_Subscriptions_PlanId"" ON ""Subscriptions"" (""PlanId"");

CREATE TABLE IF NOT EXISTS ""SubscriptionFeatureOverrides"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""SubscriptionId"" uuid NOT NULL REFERENCES ""Subscriptions"" (""Id"") ON DELETE CASCADE,
    ""FeatureCode"" text NOT NULL,
    ""IsEnabled"" boolean NOT NULL DEFAULT TRUE,
    ""CustomValue"" text,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);

CREATE TABLE IF NOT EXISTS ""Payments"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""TenantId"" uuid NOT NULL REFERENCES ""Tenants"" (""Id"") ON DELETE RESTRICT,
    ""SubscriptionId"" uuid NOT NULL REFERENCES ""Subscriptions"" (""Id"") ON DELETE CASCADE,
    ""ProviderPaymentId"" text,
    ""Amount"" numeric NOT NULL,
    ""Status"" integer NOT NULL,
    ""DueDate"" timestamp with time zone,
    ""PaidAt"" timestamp with time zone,
    ""PaymentMethod"" integer NOT NULL,
    ""Provider"" text NOT NULL DEFAULT 'Asaas',
    ""InvoiceUrl"" text,
    ""PixQrCodeUrl"" text,
    ""PixCopyPasteCode"" text,
    ""CardBrand"" text,
    ""CardLastFour"" text,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);
CREATE INDEX IF NOT EXISTS ""IX_Payments_TenantId"" ON ""Payments"" (""TenantId"");
CREATE INDEX IF NOT EXISTS ""IX_Payments_SubscriptionId"" ON ""Payments"" (""SubscriptionId"");
CREATE INDEX IF NOT EXISTS ""IX_Payments_ProviderPaymentId"" ON ""Payments"" (""ProviderPaymentId"");

CREATE TABLE IF NOT EXISTS ""PaymentWebhookEvents"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""Provider"" text NOT NULL DEFAULT 'Asaas',
    ""ProviderEventId"" text NOT NULL,
    ""EventType"" text NOT NULL,
    ""Payload"" text NOT NULL,
    ""ReceivedAt"" timestamp with time zone NOT NULL,
    ""ProcessedAt"" timestamp with time zone,
    ""Status"" text NOT NULL DEFAULT 'Received',
    ""Error"" text,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_PaymentWebhookEvents_Provider_ProviderEventId"" ON ""PaymentWebhookEvents"" (""Provider"", ""ProviderEventId"");

ALTER TABLE ""Subscriptions"" ADD COLUMN IF NOT EXISTS ""ProviderPaymentLinkId"" text;
ALTER TABLE ""Subscriptions"" ADD COLUMN IF NOT EXISTS ""ProviderCheckoutUrl"" text;
ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""ProviderPaymentLinkId"" text;
";
                await context.Database.ExecuteSqlRawAsync(sql);
            }
            else
            {
                // SQLite or InMemory fallback
                await context.Database.EnsureCreatedAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EnsureTablesCreatedAsync] Aviso/Erro ao verificar tabelas: {ex.Message}");
        }
    }
}
