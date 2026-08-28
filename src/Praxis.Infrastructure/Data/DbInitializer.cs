using Microsoft.EntityFrameworkCore;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // If database already initialized, ensure name is updated to Dra. Jamily Pinto
        if (await context.Tenants.IgnoreQueryFilters().AnyAsync())
        {
            var existingUsers = await context.Users.IgnoreQueryFilters()
                .Where(u => u.Email == "admin@nutrivida.com" || u.Email == "carla.nutri@nutrivida.com")
                .ToListAsync();

            foreach (var user in existingUsers)
            {
                user.Name = "Dra. Jamily Pinto";
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
}
