using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Application.Services;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using Xunit;
using Xunit.Abstractions;

namespace Praxis.Billing.Tests;

public class VisitStartTests
{
    private readonly ITestOutputHelper _output;

    public VisitStartTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task StartVisitAsync_ShouldNotThrow()
    {
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using (connection)
        {
            var tenantId = currentUserMock.Object.TenantId!.Value;
            var entitlementServiceMock = new Mock<IEntitlementService>();

            var clientService = new ClientService(context, currentUserMock.Object, entitlementServiceMock.Object);
            var unitService = new UnitService(context, currentUserMock.Object);
            var nutritionistService = new NutritionistService(context, currentUserMock.Object, entitlementServiceMock.Object);
            var visitService = new VisitService(context, currentUserMock.Object);

            var client = await clientService.CreateAsync(new CreateClientCompanyRequest("Cliente A", "Cliente A", "11.222.333/0001-99", "c@a.com", "119999", null, null, null));
            var unit = await unitService.CreateAsync(new CreateUnitRequest(client.Id, "Unidade 1", "Rua 1", "119999", "Resp", null));
            var nutri = await nutritionistService.CreateAsync(new CreateNutritionistRequest("Nutri 1", "n1@praxis.com", "Senha@123", "CRN-1", "119999", null));

            var defaultChecklist = new Checklist
            {
                TenantId = tenantId,
                Name = "Checklist Padrão",
                Description = "Desc",
                Status = CommonStatus.Active,
                Items = new List<ChecklistItem>
                {
                    new() { Category = "Higiene", Description = "Item 1", Order = 1, Required = true },
                    new() { Category = "Manipulação", Description = "Item 2", Order = 2, Required = true }
                }
            };
            context.Checklists.Add(defaultChecklist);
            await context.SaveChangesAsync();

            var visit = await visitService.CreateAsync(new CreateVisitRequest(unit.Id, nutri.Id, null, DateTime.UtcNow, "Notes"));
            visit.Should().NotBeNull();

            try
            {
                var started = await visitService.StartVisitAsync(visit.Id);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                foreach (var entry in ex.Entries)
                {
                    _output.WriteLine($"Concurrency Entity: {entry.Entity.GetType().Name}, State: {entry.State}, PK: {entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue}");
                }
                throw;
            }
        }
    }

    [Fact]
    public async Task CreateAsync_WhenPassingUserIdAsNutritionistId_ShouldResolveNutritionistAndSetNutritionistId()
    {
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using (connection)
        {
            var entitlementServiceMock = new Mock<IEntitlementService>();
            var clientService = new ClientService(context, currentUserMock.Object, entitlementServiceMock.Object);
            var unitService = new UnitService(context, currentUserMock.Object);
            var nutritionistService = new NutritionistService(context, currentUserMock.Object, entitlementServiceMock.Object);
            var visitService = new VisitService(context, currentUserMock.Object);

            var client = await clientService.CreateAsync(new CreateClientCompanyRequest("Cliente B", "Cliente B", "22.333.444/0001-88", "c@b.com", "119999", null, null, null));
            var unit = await unitService.CreateAsync(new CreateUnitRequest(client.Id, "Unidade 2", "Rua 2", "119999", "Resp", null));
            var nutri = await nutritionistService.CreateAsync(new CreateNutritionistRequest("Nutri 2", "n2@praxis.com", "Senha@123", "CRN-2", "119999", null));

            // Act: Pass nutri.UserId instead of nutri.Id (simulating mobile behavior when sending user.id)
            var visit = await visitService.CreateAsync(new CreateVisitRequest(unit.Id, nutri.UserId, null, DateTime.UtcNow, "Notes"));

            // Assert: Visit is created and NutritionistId correctly points to the Nutritionist.Id
            visit.Should().NotBeNull();
            visit.NutritionistId.Should().Be(nutri.Id);

            var visitInDb = await context.Visits.FindAsync(visit.Id);
            visitInDb!.NutritionistId.Should().Be(nutri.Id);
        }
    }

    [Fact]
    public async Task CancelVisitAsync_ShouldChangeStatusToCancelled()
    {
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using (connection)
        {
            var entitlementServiceMock = new Mock<IEntitlementService>();
            var clientService = new ClientService(context, currentUserMock.Object, entitlementServiceMock.Object);
            var unitService = new UnitService(context, currentUserMock.Object);
            var nutritionistService = new NutritionistService(context, currentUserMock.Object, entitlementServiceMock.Object);
            var visitService = new VisitService(context, currentUserMock.Object);

            var client = await clientService.CreateAsync(new CreateClientCompanyRequest("Cliente C", "Cliente C", "33.444.555/0001-77", "c@c.com", "119999", null, null, null));
            var unit = await unitService.CreateAsync(new CreateUnitRequest(client.Id, "Unidade 3", "Rua 3", "119999", "Resp", null));
            var nutri = await nutritionistService.CreateAsync(new CreateNutritionistRequest("Nutri 3", "n3@praxis.com", "Senha@123", "CRN-3", "119999", null));

            var visit = await visitService.CreateAsync(new CreateVisitRequest(unit.Id, nutri.Id, null, DateTime.UtcNow, "Notes"));
            visit.Status.Should().Be(VisitStatus.Scheduled);

            var cancelled = await visitService.CancelVisitAsync(visit.Id, "Cliente solicitou reagendamento");
            cancelled.Status.Should().Be(VisitStatus.Cancelled);
            cancelled.Notes.Should().Contain("Cliente solicitou reagendamento");
        }
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteVisit()
    {
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using (connection)
        {
            var entitlementServiceMock = new Mock<IEntitlementService>();
            var clientService = new ClientService(context, currentUserMock.Object, entitlementServiceMock.Object);
            var unitService = new UnitService(context, currentUserMock.Object);
            var nutritionistService = new NutritionistService(context, currentUserMock.Object, entitlementServiceMock.Object);
            var visitService = new VisitService(context, currentUserMock.Object);

            var client = await clientService.CreateAsync(new CreateClientCompanyRequest("Cliente D", "Cliente D", "44.555.666/0001-66", "c@d.com", "119999", null, null, null));
            var unit = await unitService.CreateAsync(new CreateUnitRequest(client.Id, "Unidade 4", "Rua 4", "119999", "Resp", null));
            var nutri = await nutritionistService.CreateAsync(new CreateNutritionistRequest("Nutri 4", "n4@praxis.com", "Senha@123", "CRN-4", "119999", null));

            var visit = await visitService.CreateAsync(new CreateVisitRequest(unit.Id, nutri.Id, null, DateTime.UtcNow, "Notes"));

            await visitService.DeleteAsync(visit.Id);

            var inDb = await context.Visits.IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Id == visit.Id);
            inDb!.IsDeleted.Should().BeTrue();
            inDb.DeletedAt.Should().NotBeNull();
        }
    }
}
