using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using Praxis.Infrastructure.Data;

namespace Praxis.Billing.Tests;

public static class TestDbContextFactory
{
    public static (ApplicationDbContext Context, Mock<ICurrentUserService> CurrentUserMock, SqliteConnection Connection) CreateInMemoryDbContext(Guid? tenantId = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var currentUserMock = new Mock<ICurrentUserService>();
        var currentTenantId = tenantId ?? Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        currentUserMock.Setup(c => c.TenantId).Returns(currentTenantId);
        currentUserMock.Setup(c => c.UserId).Returns(currentUserId);
        currentUserMock.Setup(c => c.Role).Returns(UserRole.TenantAdmin);
        currentUserMock.Setup(c => c.UserEmail).Returns("admin@tenant.com");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ApplicationDbContext(options, currentUserMock.Object);
        context.Database.EnsureCreated();

        // Seed Plans
        if (!context.Plans.Any())
        {
            var planEssential = new Plan
            {
                Id = Guid.NewGuid(),
                Name = "PRAXIS Essencial",
                Code = "essential",
                Description = "Plano Essencial",
                MonthlyPrice = 149.00m,
                AnnualPrice = 1490.00m,
                MaxNutritionists = 3,
                MaxClientCompanies = 10,
                MaxStorageMb = 1000,
                IsActive = true,
                Features = new List<PlanFeature>
                {
                    new() { Id = Guid.NewGuid(), FeatureCode = "dashboard", IsEnabled = true },
                    new() { Id = Guid.NewGuid(), FeatureCode = "clients", IsEnabled = true },
                    new() { Id = Guid.NewGuid(), FeatureCode = "nutritionists", IsEnabled = true },
                    new() { Id = Guid.NewGuid(), FeatureCode = "pdf_export", IsEnabled = true }
                }
            };

            var planProfessional = new Plan
            {
                Id = Guid.NewGuid(),
                Name = "PRAXIS Profissional",
                Code = "professional",
                Description = "Plano Profissional",
                MonthlyPrice = 299.00m,
                AnnualPrice = 2990.00m,
                MaxNutritionists = 10,
                MaxClientCompanies = 50,
                MaxStorageMb = 5000,
                IsActive = true,
                Features = new List<PlanFeature>
                {
                    new() { Id = Guid.NewGuid(), FeatureCode = "dashboard", IsEnabled = true },
                    new() { Id = Guid.NewGuid(), FeatureCode = "clients", IsEnabled = true },
                    new() { Id = Guid.NewGuid(), FeatureCode = "nutritionists", IsEnabled = true },
                    new() { Id = Guid.NewGuid(), FeatureCode = "pdf_export", IsEnabled = true },
                    new() { Id = Guid.NewGuid(), FeatureCode = "advanced_analytics", IsEnabled = true },
                    new() { Id = Guid.NewGuid(), FeatureCode = "excel_export", IsEnabled = true }
                }
            };

            var planEnterprise = new Plan
            {
                Id = Guid.NewGuid(),
                Name = "PRAXIS Enterprise",
                Code = "enterprise",
                Description = "Plano Enterprise",
                MonthlyPrice = 0.00m,
                AnnualPrice = 0.00m,
                MaxNutritionists = 999,
                MaxClientCompanies = 999,
                MaxStorageMb = 50000,
                IsActive = true,
                Features = new List<PlanFeature>
                {
                    new() { Id = Guid.NewGuid(), FeatureCode = "dashboard", IsEnabled = true },
                    new() { Id = Guid.NewGuid(), FeatureCode = "advanced_analytics", IsEnabled = true },
                    new() { Id = Guid.NewGuid(), FeatureCode = "custom_integrations", IsEnabled = true }
                }
            };

            context.Plans.AddRange(planEssential, planProfessional, planEnterprise);

            var tenant = new Tenant
            {
                Id = currentTenantId,
                Name = "Consultoria Nutri Teste",
                LegalName = "Consultoria Nutri Teste Ltda",
                Cnpj = "11.222.333/0001-44",
                Email = "admin@tenant.com",
                Phone = "(11) 99999-8888",
                Status = TenantStatus.Active
            };
            context.Tenants.Add(tenant);

            var user = new User
            {
                Id = currentUserId,
                TenantId = currentTenantId,
                Name = "Dra. Nutricionista Teste",
                Email = "admin@tenant.com",
                PasswordHash = "hash",
                Role = UserRole.TenantAdmin,
                Status = UserStatus.Active
            };
            context.Users.Add(user);

            var sub = new Subscription
            {
                Id = Guid.NewGuid(),
                TenantId = currentTenantId,
                PlanId = planProfessional.Id,
                Plan = planProfessional,
                Status = SubscriptionStatus.Trial,
                BillingCycle = BillingCycle.Monthly,
                StartedAt = DateTime.UtcNow,
                TrialEndsAt = DateTime.UtcNow.AddDays(14),
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
                PaymentProvider = "Asaas"
            };
            context.Subscriptions.Add(sub);

            context.SaveChanges();
        }

        return (context, currentUserMock, connection);
    }
}
