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

        // If database already initialized, ensure name is updated to Dra. Jamily Pinto and ensure subscriptions exist
        if (await context.Tenants.IgnoreQueryFilters().AnyAsync())
        {
            var existingUsers = await context.Users.IgnoreQueryFilters()
                .Where(u => u.Email == "admin@nutrivida.com" || u.Email == "carla.nutri@nutrivida.com")
                .ToListAsync();

            foreach (var user in existingUsers)
            {
                user.Name = "Dra. Jamily Pinto";
            }

            var defaultPlan = await context.Plans.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Code == "professional");
            if (defaultPlan != null)
            {
                var tenantsWithoutSub = await context.Tenants.IgnoreQueryFilters()
                    .Where(t => !context.Subscriptions.IgnoreQueryFilters().Any(s => s.TenantId == t.Id))
                    .ToListAsync();

                foreach (var t in tenantsWithoutSub)
                {
                    context.Subscriptions.Add(new Subscription
                    {
                        TenantId = t.Id,
                        PlanId = defaultPlan.Id,
                        Status = SubscriptionStatus.Trial,
                        BillingCycle = BillingCycle.Monthly,
                        StartedAt = DateTime.UtcNow,
                        TrialEndsAt = DateTime.UtcNow.AddDays(14),
                        CurrentPeriodStart = DateTime.UtcNow,
                        CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
                    });
                }
            }

            await context.SaveChangesAsync();
            return;
        }

        var tenant = new Tenant
        {
            Name = "NutriVida Assessoria",
            LegalName = "NutriVida Assessoria Nutricional Ltda",
            Cnpj = "12.345.678/0001-90",
            Email = "contato@nutrivida.com",
            Phone = "(11) 3456-7890",
            Status = TenantStatus.Active
        };
        context.Tenants.Add(tenant);

        var adminUser = new User
        {
            TenantId = tenant.Id,
            Name = "Dra. Jamily Pinto",
            Email = "admin@nutrivida.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Praxis@123"),
            Role = UserRole.TenantAdmin,
            Status = UserStatus.Active
        };
        context.Users.Add(adminUser);

        var nutritionist = new Nutritionist
        {
            TenantId = tenant.Id,
            UserId = adminUser.Id,
            Crn = "CRN-3/45892",
            Phone = "(11) 98765-4321",
            Status = CommonStatus.Active
        };
        context.Nutritionists.Add(nutritionist);

        var client1 = new ClientCompany
        {
            TenantId = tenant.Id,
            LegalName = "Restaurante Sabor Caseiro Ltda",
            TradeName = "Sabor Caseiro Gastronomia",
            Cnpj = "98.765.432/0001-10",
            Email = "gerencia@saborcaseiro.com.br",
            Phone = "(11) 3322-1100",
            Address = "Av. Paulista, 1000 - Bela Vista, São Paulo - SP",
            ResponsibleName = "Carlos Eduardo",
            Status = CommonStatus.Active
        };
        context.ClientCompanies.Add(client1);

        var unit1 = new Unit
        {
            TenantId = tenant.Id,
            ClientCompanyId = client1.Id,
            Name = "Unidade Jardins",
            Address = "Rua Oscar Freire, 500 - Jardins, São Paulo - SP",
            Phone = "(11) 3322-1101",
            ResponsibleName = "Marcos Vinicius (Gerente)",
            Status = CommonStatus.Active
        };
        var unit2 = new Unit
        {
            TenantId = tenant.Id,
            ClientCompanyId = client1.Id,
            Name = "Unidade Paulista",
            Address = "Av. Paulista, 1000 - Bela Vista, São Paulo - SP",
            Phone = "(11) 3322-1102",
            ResponsibleName = "Fernanda Souza",
            Status = CommonStatus.Active
        };
        context.Units.AddRange(unit1, unit2);

        var art = new ART
        {
            TenantId = tenant.Id,
            UnitId = unit1.Id,
            NutritionistId = nutritionist.Id,
            Number = "ART-SP-2026/00142",
            StartDate = DateTime.UtcNow.AddMonths(-6),
            EndDate = DateTime.UtcNow.AddMonths(6),
            Status = ArtStatus.Active,
            Notes = "Responsabilidade técnica integral com visitas quinzenais."
        };
        context.ARTs.Add(art);

        var checklist = new Checklist
        {
            TenantId = tenant.Id,
            Name = "Checklist Padrão RDC 216 / Boas Práticas",
            Description = "Verificação de higienização, cadeia fria, validade e manipulação.",
            Status = CommonStatus.Active,
            Items = new List<ChecklistItem>
            {
                new() { Category = "Higiene Pessoal", Description = "Apresentação e uniformes limpos e completos de toda a equipe", Order = 1, Required = true },
                new() { Category = "Higiene Pessoal", Description = "Lavatório exclusivo para mãos abastecido com sabonete antisséptico e papel toalha", Order = 2, Required = true },
                new() { Category = "Armazenamento", Description = "Produtos armazenados com etiquetas de identificação e validade visíveis", Order = 3, Required = true },
                new() { Category = "Armazenamento", Description = "Controle diário de temperatura de freezers e geladeiras registrado na planilha", Order = 4, Required = true },
                new() { Category = "Instalações & Equipamentos", Description = "Equipamentos e bancadas limpos, sanitizados e sem resíduos", Order = 5, Required = true },
                new() { Category = "Manipulação", Description = "Ausência de contaminação cruzada entre crus e cozidos", Order = 6, Required = true }
            }
        };
        context.Checklists.Add(checklist);

        // Pre-create assignment
        context.NutritionistUnitAssignments.Add(new NutritionistUnitAssignment
        {
            TenantId = tenant.Id,
            NutritionistId = nutritionist.Id,
            UnitId = unit1.Id
        });

        // Pre-create sample finished visit
        var visit = new Visit
        {
            TenantId = tenant.Id,
            UnitId = unit1.Id,
            NutritionistId = nutritionist.Id,
            ChecklistId = checklist.Id,
            ScheduledAt = DateTime.UtcNow.AddDays(-2),
            StartedAt = DateTime.UtcNow.AddDays(-2).AddHours(9),
            FinishedAt = DateTime.UtcNow.AddDays(-2).AddHours(11),
            Status = VisitStatus.Finished,
            Notes = "Visita técnica de rotina realizada. Equipe orientada sobre temperatura."
        };
        context.Visits.Add(visit);

        var visitItem1 = new VisitItem { VisitId = visit.Id, ChecklistItemId = checklist.Items.ElementAt(0).Id, Result = EvaluationResult.Conforme };
        var visitItem2 = new VisitItem { VisitId = visit.Id, ChecklistItemId = checklist.Items.ElementAt(1).Id, Result = EvaluationResult.Conforme };
        var visitItem3 = new VisitItem { VisitId = visit.Id, ChecklistItemId = checklist.Items.ElementAt(2).Id, Result = EvaluationResult.Conforme };
        var visitItem4 = new VisitItem { VisitId = visit.Id, ChecklistItemId = checklist.Items.ElementAt(3).Id, Result = EvaluationResult.NaoConforme, Observation = "Planilha de temperatura do freezer 2 desatualizada há 3 dias." };
        var visitItem5 = new VisitItem { VisitId = visit.Id, ChecklistItemId = checklist.Items.ElementAt(4).Id, Result = EvaluationResult.Conforme };
        var visitItem6 = new VisitItem { VisitId = visit.Id, ChecklistItemId = checklist.Items.ElementAt(5).Id, Result = EvaluationResult.Conforme };

        context.VisitItems.AddRange(visitItem1, visitItem2, visitItem3, visitItem4, visitItem5, visitItem6);

        var nc = new NonConformity
        {
            TenantId = tenant.Id,
            VisitId = visit.Id,
            VisitItemId = visitItem4.Id,
            Category = "Armazenamento",
            Description = "Planilha de controle térmico do freezer principal desatualizada",
            Severity = NonConformitySeverity.Media,
            Status = NonConformityStatus.Aberta,
            DueDate = DateTime.UtcNow.AddDays(5),
            CorrectiveAction = "Reorientar o estoquista responsável e realizar medição duas vezes ao dia."
        };
        context.NonConformities.Add(nc);

        await context.SaveChangesAsync();
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
