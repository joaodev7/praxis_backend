using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context, ILogger? logger = null)
    {
        await EnsureTablesCreatedAsync(context, logger);

        // 1. Seed or Update Plans to match current commercial catalog
        var existingPlans = await context.Plans.IgnoreQueryFilters().Include(p => p.Features).ToListAsync();

        // 1.1 Plano Profissional Autônomo (essential)
        var planEssential = existingPlans.FirstOrDefault(p => p.Code.Equals("essential", StringComparison.OrdinalIgnoreCase));
        if (planEssential == null)
        {
            planEssential = new Plan { Code = "essential" };
            context.Plans.Add(planEssential);
        }
        planEssential.Name = "Profissional Autônomo";
        planEssential.Description = "Ideal para nutricionistas RTs autônomos que atendem até 5 estabelecimentos.";
        planEssential.MonthlyPrice = 149.00m;
        planEssential.AnnualPrice = 1490.00m;
        planEssential.MaxNutritionists = 1;
        planEssential.MaxClientCompanies = 5;
        planEssential.MaxStorageMb = 1000;
        planEssential.IsActive = true;
        SyncPlanFeatures(planEssential, new[]
        {
            "dashboard", "clients", "units", "nutritionists", "arts", "visits", "checklists", "photos", "pdf_export", "food_labels"
        });

        // 1.2 Plano Consultoria Pro (professional)
        var planProfessional = existingPlans.FirstOrDefault(p => p.Code.Equals("professional", StringComparison.OrdinalIgnoreCase));
        if (planProfessional == null)
        {
            planProfessional = new Plan { Code = "professional" };
            context.Plans.Add(planProfessional);
        }
        planProfessional.Name = "Consultoria Pro";
        planProfessional.Description = "Para consultorias em expansão com múltiplos clientes e equipe de nutricionistas.";
        planProfessional.MonthlyPrice = 299.00m;
        planProfessional.AnnualPrice = 2990.00m;
        planProfessional.MaxNutritionists = 5;
        planProfessional.MaxClientCompanies = 25;
        planProfessional.MaxStorageMb = 5000;
        planProfessional.IsActive = true;
        SyncPlanFeatures(planProfessional, new[]
        {
            "dashboard", "clients", "units", "nutritionists", "arts", "visits", "checklists", "photos", "pdf_export",
            "advanced_analytics", "period_comparison", "excel_export", "custom_reports", "priority_support",
            "food_labels", "action_plans", "geolocation", "public_labels_qr"
        });

        // 1.3 Plano Consultoria Escala (enterprise)
        var planEnterprise = existingPlans.FirstOrDefault(p => p.Code.Equals("enterprise", StringComparison.OrdinalIgnoreCase));
        if (planEnterprise == null)
        {
            planEnterprise = new Plan { Code = "enterprise" };
            context.Plans.Add(planEnterprise);
        }
        planEnterprise.Name = "Consultoria Escala";
        planEnterprise.Description = "Para grandes consultorias, redes de franquias e empresas de alimentação coletiva.";
        planEnterprise.MonthlyPrice = 549.00m;
        planEnterprise.AnnualPrice = 5490.00m;
        planEnterprise.MaxNutritionists = 999;
        planEnterprise.MaxClientCompanies = 999;
        planEnterprise.MaxStorageMb = 50000;
        planEnterprise.IsActive = true;
        SyncPlanFeatures(planEnterprise, new[]
        {
            "dashboard", "clients", "units", "nutritionists", "arts", "visits", "checklists", "photos", "pdf_export",
            "advanced_analytics", "period_comparison", "excel_export", "custom_reports", "priority_support",
            "food_labels", "action_plans", "geolocation", "public_labels_qr",
            "dedicated_support", "custom_integrations", "sla_guarantee"
        });

        await context.SaveChangesAsync();
    }

    private static async Task EnsureTablesCreatedAsync(ApplicationDbContext context, ILogger? logger = null)
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
            logger?.LogWarning(ex, "[EnsureTablesCreatedAsync] Aviso/Erro ao verificar tabelas: {Message}", ex.Message);
        }
    }

    private static void SyncPlanFeatures(Plan plan, string[] featureCodes)
    {
        plan.Features ??= new List<PlanFeature>();
        foreach (var code in featureCodes)
        {
            if (!plan.Features.Any(f => f.FeatureCode.Equals(code, StringComparison.OrdinalIgnoreCase)))
            {
                plan.Features.Add(new PlanFeature { FeatureCode = code, IsEnabled = true });
            }
        }
    }
}
