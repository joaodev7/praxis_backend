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
}
