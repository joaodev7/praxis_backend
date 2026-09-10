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

CREATE TABLE IF NOT EXISTS ""Files"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""TenantId"" uuid NOT NULL REFERENCES ""Tenants"" (""Id"") ON DELETE RESTRICT,
    ""UploadedByUserId"" uuid REFERENCES ""Users"" (""Id"") ON DELETE SET NULL,
    ""ClientId"" uuid REFERENCES ""ClientCompanies"" (""Id"") ON DELETE SET NULL,
    ""OriginalFileName"" text NOT NULL,
    ""ObjectKey"" text NOT NULL,
    ""ContentType"" text NOT NULL,
    ""Size"" bigint NOT NULL,
    ""Category"" integer NOT NULL,
    ""Status"" integer NOT NULL,
    ""UploadedAt"" timestamp with time zone,
    ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
    ""DeletedAt"" timestamp with time zone,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);
CREATE INDEX IF NOT EXISTS ""IX_Files_TenantId"" ON ""Files"" (""TenantId"");
CREATE INDEX IF NOT EXISTS ""IX_Files_ClientId"" ON ""Files"" (""ClientId"");
CREATE INDEX IF NOT EXISTS ""IX_Files_ObjectKey"" ON ""Files"" (""ObjectKey"");
CREATE INDEX IF NOT EXISTS ""IX_Files_Status"" ON ""Files"" (""Status"");

ALTER TABLE ""Subscriptions"" ADD COLUMN IF NOT EXISTS ""ProviderPaymentLinkId"" text;
ALTER TABLE ""Subscriptions"" ADD COLUMN IF NOT EXISTS ""ProviderCheckoutUrl"" text;
ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""ProviderPaymentLinkId"" text;
ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""DateOfBirth"" date;
ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""ProfilePhotoKey"" text;
ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""ProfilePhotoUrl"" text;

ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""TenantId"" uuid;
ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""What"" text;
ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""Why"" text;
ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""ResponsibleName"" text;
ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""Where"" text;
ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""How"" text;
ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""HowMuch"" numeric;
ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""Priority"" integer NOT NULL DEFAULT 2;
ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""StartedAt"" timestamp with time zone;
ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""ValidatedAt"" timestamp with time zone;
ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""ValidatedByUserId"" uuid;
ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""ValidationComment"" text;
ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""CancellationReason"" text;

CREATE TABLE IF NOT EXISTS ""ActionPlanEvidences"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""TenantId"" uuid NOT NULL REFERENCES ""Tenants"" (""Id"") ON DELETE RESTRICT,
    ""ActionPlanId"" uuid NOT NULL REFERENCES ""ActionItems"" (""Id"") ON DELETE CASCADE,
    ""FileUrl"" text NOT NULL,
    ""ObjectKey"" text NOT NULL,
    ""FileName"" text NOT NULL,
    ""ContentType"" text NOT NULL,
    ""FileSize"" bigint NOT NULL,
    ""UploadedAt"" timestamp with time zone NOT NULL,
    ""UploadedByUserId"" uuid REFERENCES ""Users"" (""Id"") ON DELETE SET NULL,
    ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
    ""DeletedAt"" timestamp with time zone,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);
CREATE INDEX IF NOT EXISTS ""IX_ActionPlanEvidences_ActionPlanId"" ON ""ActionPlanEvidences"" (""ActionPlanId"");
CREATE INDEX IF NOT EXISTS ""IX_ActionPlanEvidences_TenantId"" ON ""ActionPlanEvidences"" (""TenantId"");
CREATE INDEX IF NOT EXISTS ""IX_ActionPlanEvidences_ObjectKey"" ON ""ActionPlanEvidences"" (""ObjectKey"");

-- ==========================================================
-- ETiQUETAGEM & GESTÃO DE VALIDADE (RDC 216/2004)
-- ==========================================================
CREATE TABLE IF NOT EXISTS ""LabelTemplates"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""TenantId"" uuid NOT NULL REFERENCES ""Tenants"" (""Id"") ON DELETE CASCADE,
    ""Name"" text NOT NULL,
    ""TemplateType"" text NOT NULL,
    ""WidthMm"" numeric NOT NULL,
    ""HeightMm"" numeric NOT NULL,
    ""IncludeQrCode"" boolean NOT NULL DEFAULT TRUE,
    ""IncludeLogo"" boolean NOT NULL DEFAULT FALSE,
    ""IsDefault"" boolean NOT NULL DEFAULT FALSE,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);
CREATE INDEX IF NOT EXISTS ""IX_LabelTemplates_TenantId_TemplateType"" ON ""LabelTemplates"" (""TenantId"", ""TemplateType"");

CREATE TABLE IF NOT EXISTS ""Products"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""TenantId"" uuid NOT NULL REFERENCES ""Tenants"" (""Id"") ON DELETE RESTRICT,
    ""UnitId"" uuid REFERENCES ""Units"" (""Id"") ON DELETE SET NULL,
    ""Name"" text NOT NULL,
    ""Description"" text,
    ""Category"" text,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
    ""DeletedAt"" timestamp with time zone,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);
CREATE INDEX IF NOT EXISTS ""IX_Products_TenantId_Category"" ON ""Products"" (""TenantId"", ""Category"");
CREATE INDEX IF NOT EXISTS ""IX_Products_TenantId_UnitId"" ON ""Products"" (""TenantId"", ""UnitId"");
CREATE INDEX IF NOT EXISTS ""IX_Products_UnitId"" ON ""Products"" (""UnitId"");

CREATE TABLE IF NOT EXISTS ""ProductBatches"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""TenantId"" uuid NOT NULL REFERENCES ""Tenants"" (""Id"") ON DELETE RESTRICT,
    ""ProductId"" uuid NOT NULL REFERENCES ""Products"" (""Id"") ON DELETE CASCADE,
    ""BatchCode"" text NOT NULL,
    ""OriginalBatchCode"" text,
    ""ManufacturingDate"" timestamp with time zone,
    ""OriginalExpirationDate"" timestamp with time zone,
    ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
    ""DeletedAt"" timestamp with time zone,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);
CREATE INDEX IF NOT EXISTS ""IX_ProductBatches_ProductId"" ON ""ProductBatches"" (""ProductId"");
CREATE INDEX IF NOT EXISTS ""IX_ProductBatches_TenantId_BatchCode"" ON ""ProductBatches"" (""TenantId"", ""BatchCode"");
CREATE INDEX IF NOT EXISTS ""IX_ProductBatches_TenantId_ProductId"" ON ""ProductBatches"" (""TenantId"", ""ProductId"");

CREATE TABLE IF NOT EXISTS ""ValidityRules"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""TenantId"" uuid NOT NULL REFERENCES ""Tenants"" (""Id"") ON DELETE RESTRICT,
    ""UnitId"" uuid REFERENCES ""Units"" (""Id"") ON DELETE SET NULL,
    ""ProductId"" uuid REFERENCES ""Products"" (""Id"") ON DELETE SET NULL,
    ""Name"" text NOT NULL,
    ""Description"" text,
    ""ProductCategory"" text,
    ""LabelType"" integer,
    ""OperationType"" integer,
    ""StorageCondition"" integer,
    ""MaximumTemperature"" numeric,
    ""ValidityValue"" integer NOT NULL,
    ""ValidityUnit"" integer NOT NULL,
    ""AllowManualExpiration"" boolean NOT NULL DEFAULT FALSE,
    ""RequiresTechnicalBasis"" boolean NOT NULL DEFAULT FALSE,
    ""TechnicalBasis"" text,
    ""RegulatoryReference"" text,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
    ""DeletedAt"" timestamp with time zone,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);
CREATE INDEX IF NOT EXISTS ""IX_ValidityRules_ProductId"" ON ""ValidityRules"" (""ProductId"");
CREATE INDEX IF NOT EXISTS ""IX_ValidityRules_TenantId_IsActive_UnitId_ProductId"" ON ""ValidityRules"" (""TenantId"", ""IsActive"", ""UnitId"", ""ProductId"");
CREATE INDEX IF NOT EXISTS ""IX_ValidityRules_UnitId"" ON ""ValidityRules"" (""UnitId"");

CREATE TABLE IF NOT EXISTS ""FoodLabels"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""TenantId"" uuid NOT NULL REFERENCES ""Tenants"" (""Id"") ON DELETE RESTRICT,
    ""UnitId"" uuid NOT NULL REFERENCES ""Units"" (""Id"") ON DELETE RESTRICT,
    ""ProductId"" uuid NOT NULL REFERENCES ""Products"" (""Id"") ON DELETE RESTRICT,
    ""ProductBatchId"" uuid REFERENCES ""ProductBatches"" (""Id"") ON DELETE SET NULL,
    ""ValidityRuleId"" uuid REFERENCES ""ValidityRules"" (""Id"") ON DELETE SET NULL,
    ""LabelType"" integer NOT NULL,
    ""OperationType"" integer NOT NULL,
    ""Status"" integer NOT NULL,
    ""Description"" text NOT NULL,
    ""InternalBatchCode"" text NOT NULL,
    ""ManufacturedAt"" timestamp with time zone,
    ""PreparedAt"" timestamp with time zone,
    ""OpenedAt"" timestamp with time zone,
    ""PortionedAt"" timestamp with time zone,
    ""ValidityStartAt"" timestamp with time zone NOT NULL,
    ""CalculatedExpirationDate"" timestamp with time zone NOT NULL,
    ""ManualExpirationDate"" timestamp with time zone,
    ""ValiditySource"" integer NOT NULL,
    ""ValidityJustification"" text,
    ""StorageCondition"" integer NOT NULL,
    ""StorageTemperatureMin"" numeric,
    ""StorageTemperatureMax"" numeric,
    ""StorageInstructions"" text,
    ""PublicToken"" text NOT NULL,
    ""PrintCount"" integer NOT NULL DEFAULT 0,
    ""LastPrintedAt"" timestamp with time zone,
    ""CreatedByUserId"" uuid NOT NULL REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT,
    ""CancelledByUserId"" uuid REFERENCES ""Users"" (""Id"") ON DELETE SET NULL,
    ""CancelledAt"" timestamp with time zone,
    ""CancellationReason"" text,
    ""DiscardedByUserId"" uuid REFERENCES ""Users"" (""Id"") ON DELETE SET NULL,
    ""DiscardedAt"" timestamp with time zone,
    ""DiscardReason"" text,
    ""DiscardQuantity"" numeric,
    ""DiscardUnit"" text,
    ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
    ""DeletedAt"" timestamp with time zone,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_FoodLabels_PublicToken"" ON ""FoodLabels"" (""PublicToken"");
CREATE INDEX IF NOT EXISTS ""IX_FoodLabels_TenantId_InternalBatchCode"" ON ""FoodLabels"" (""TenantId"", ""InternalBatchCode"");
CREATE INDEX IF NOT EXISTS ""IX_FoodLabels_TenantId_CalculatedExpirationDate"" ON ""FoodLabels"" (""TenantId"", ""CalculatedExpirationDate"");
CREATE INDEX IF NOT EXISTS ""IX_FoodLabels_TenantId_UnitId"" ON ""FoodLabels"" (""TenantId"", ""UnitId"");
CREATE INDEX IF NOT EXISTS ""IX_FoodLabels_TenantId_ProductId"" ON ""FoodLabels"" (""TenantId"", ""ProductId"");
CREATE INDEX IF NOT EXISTS ""IX_FoodLabels_UnitId"" ON ""FoodLabels"" (""UnitId"");
CREATE INDEX IF NOT EXISTS ""IX_FoodLabels_ProductId"" ON ""FoodLabels"" (""ProductId"");
CREATE INDEX IF NOT EXISTS ""IX_FoodLabels_ProductBatchId"" ON ""FoodLabels"" (""ProductBatchId"");
CREATE INDEX IF NOT EXISTS ""IX_FoodLabels_ValidityRuleId"" ON ""FoodLabels"" (""ValidityRuleId"");
CREATE INDEX IF NOT EXISTS ""IX_FoodLabels_CreatedByUserId"" ON ""FoodLabels"" (""CreatedByUserId"");
CREATE INDEX IF NOT EXISTS ""IX_FoodLabels_CancelledByUserId"" ON ""FoodLabels"" (""CancelledByUserId"");
CREATE INDEX IF NOT EXISTS ""IX_FoodLabels_DiscardedByUserId"" ON ""FoodLabels"" (""DiscardedByUserId"");

CREATE TABLE IF NOT EXISTS ""LabelPrints"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""TenantId"" uuid NOT NULL REFERENCES ""Tenants"" (""Id"") ON DELETE CASCADE,
    ""LabelId"" uuid NOT NULL REFERENCES ""FoodLabels"" (""Id"") ON DELETE CASCADE,
    ""PrintedByUserId"" uuid NOT NULL REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT,
    ""PrintedAt"" timestamp with time zone NOT NULL,
    ""Quantity"" integer NOT NULL DEFAULT 1,
    ""PrinterName"" text,
    ""TemplateUsed"" text,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);
CREATE INDEX IF NOT EXISTS ""IX_LabelPrints_LabelId"" ON ""LabelPrints"" (""LabelId"");
CREATE INDEX IF NOT EXISTS ""IX_LabelPrints_TenantId_LabelId"" ON ""LabelPrints"" (""TenantId"", ""LabelId"");
CREATE INDEX IF NOT EXISTS ""IX_LabelPrints_PrintedByUserId"" ON ""LabelPrints"" (""PrintedByUserId"");

CREATE TABLE IF NOT EXISTS ""FoodLabelAudits"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""TenantId"" uuid NOT NULL REFERENCES ""Tenants"" (""Id"") ON DELETE CASCADE,
    ""LabelId"" uuid NOT NULL REFERENCES ""FoodLabels"" (""Id"") ON DELETE CASCADE,
    ""Action"" text NOT NULL,
    ""OldValue"" text,
    ""NewValue"" text,
    ""UserId"" uuid REFERENCES ""Users"" (""Id"") ON DELETE SET NULL,
    ""Details"" text,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone
);
CREATE INDEX IF NOT EXISTS ""IX_FoodLabelAudits_LabelId"" ON ""FoodLabelAudits"" (""LabelId"");
CREATE INDEX IF NOT EXISTS ""IX_FoodLabelAudits_TenantId_LabelId"" ON ""FoodLabelAudits"" (""TenantId"", ""LabelId"");
CREATE INDEX IF NOT EXISTS ""IX_FoodLabelAudits_UserId"" ON ""FoodLabelAudits"" (""UserId"");
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
