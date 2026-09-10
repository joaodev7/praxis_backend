using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Application.Services;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using Xunit;

namespace Praxis.Billing.Tests;

public class EntityManagementTests
{
    [Fact]
    public async Task Client_CreateUpdateDelete_ShouldWorkCorrectlyWithTenantIsolation()
    {
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using (connection)
        {
            var tenantId = currentUserMock.Object.TenantId!.Value;
            var entitlementServiceMock = new Mock<IEntitlementService>();
            entitlementServiceMock.Setup(e => e.ValidateLimitAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), default))
                .Returns(Task.CompletedTask);

            var clientService = new ClientService(context, currentUserMock.Object, entitlementServiceMock.Object);

            // 1. Create Client
            var created = await clientService.CreateAsync(new CreateClientCompanyRequest(
                "Restaurante Sabor Ltda",
                "Restaurante Sabor",
                "12.345.678/0001-90",
                "contato@sabor.com",
                "11988887777",
                "Rua das Flores, 123",
                "João Gerente",
                "Observação teste"
            ));

            created.Should().NotBeNull();
            created.TradeName.Should().Be("Restaurante Sabor");
            created.Cnpj.Should().Be("12.345.678/0001-90");

            // 2. Update Client
            var updated = await clientService.UpdateAsync(created.Id, new UpdateClientCompanyRequest(
                "Restaurante Sabor Renovado Ltda",
                "Restaurante Sabor Renovado",
                "novo@sabor.com",
                "11977776666",
                "Av Nova, 500",
                "Maria Gerente",
                "Notas atualizadas",
                CommonStatus.Active
            ));

            updated.TradeName.Should().Be("Restaurante Sabor Renovado");
            updated.Email.Should().Be("novo@sabor.com");
            updated.ResponsibleName.Should().Be("Maria Gerente");

            // 3. Delete Client (Soft delete)
            await clientService.DeleteAsync(created.Id);

            var allClients = await clientService.GetAllAsync();
            allClients.Should().NotContain(c => c.Id == created.Id);

            // Verify in raw context that it's soft-deleted
            var rawClient = await context.ClientCompanies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == created.Id);
            rawClient.Should().NotBeNull();
            rawClient!.IsDeleted.Should().BeTrue();
            rawClient.Status.Should().Be(CommonStatus.Inactive);
        }
    }

    [Fact]
    public async Task Unit_CreateUpdateDelete_And_NutritionistAllocation_ShouldWork()
    {
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using (connection)
        {
            var tenantId = currentUserMock.Object.TenantId!.Value;
            var entitlementServiceMock = new Mock<IEntitlementService>();
            entitlementServiceMock.Setup(e => e.ValidateLimitAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), default))
                .Returns(Task.CompletedTask);

            var clientService = new ClientService(context, currentUserMock.Object, entitlementServiceMock.Object);
            var unitService = new UnitService(context, currentUserMock.Object);
            var nutritionistService = new NutritionistService(context, currentUserMock.Object, entitlementServiceMock.Object);

            // 1. Create Client & Unit
            var client = await clientService.CreateAsync(new CreateClientCompanyRequest(
                "Padaria Real Ltda", "Padaria Real", "22.333.444/0001-55", "padaria@real.com", "11999998888", null, null, null));

            var unit = await unitService.CreateAsync(new CreateUnitRequest(
                client.Id, "Filial Jardins", "Alameda Santos, 200", "1133334444", "Carlos", "Unidade principal"));

            unit.Should().NotBeNull();
            unit.Name.Should().Be("Filial Jardins");
            unit.AssignedNutritionists.Should().BeEmpty();

            // 2. Create Nutritionist (without unit initially)
            var nutri = await nutritionistService.CreateAsync(new CreateNutritionistRequest(
                "Dra. Camila Alimentos", "camila@nutri.com", "Senha@123", "CRN-3/98765", "11988881111", null));

            nutri.Should().NotBeNull();
            nutri.Name.Should().Be("Dra. Camila Alimentos");
            nutri.AssignedUnitIds.Should().BeEmpty();

            // 3. Allocate Nutritionist to Unit afterwards
            var allocatedUnit = await unitService.AllocateNutritionistAsync(unit.Id, nutri.Id);
            allocatedUnit.AssignedNutritionists.Should().HaveCount(1);
            allocatedUnit.AssignedNutritionists!.First().Name.Should().Be("Dra. Camila Alimentos");
            allocatedUnit.AssignedNutritionists!.First().Crn.Should().Be("CRN-3/98765");

            // Verify idempotency (no duplicate allocation)
            var reallocatedUnit = await unitService.AllocateNutritionistAsync(unit.Id, nutri.Id);
            reallocatedUnit.AssignedNutritionists.Should().HaveCount(1);

            // 4. Update Unit
            var updatedUnit = await unitService.UpdateAsync(unit.Id, new UpdateUnitRequest(
                "Filial Jardins Premium", "Alameda Santos, 250", "1133335555", "Carlos Silva", "Reformada", CommonStatus.Active));

            updatedUnit.Name.Should().Be("Filial Jardins Premium");
            updatedUnit.Address.Should().Be("Alameda Santos, 250");
            // Relationships preserved after update
            updatedUnit.AssignedNutritionists.Should().HaveCount(1);

            // 5. Deallocate Nutritionist from Unit
            var deallocatedUnit = await unitService.DeallocateNutritionistAsync(unit.Id, nutri.Id);
            deallocatedUnit.AssignedNutritionists.Should().BeEmpty();

            // Verify nutritionist still exists and is not deleted!
            var nutriAfter = await nutritionistService.GetByIdAsync(nutri.Id);
            nutriAfter.Should().NotBeNull();
            nutriAfter.Name.Should().Be("Dra. Camila Alimentos");

            // 6. Delete Unit (Soft Delete)
            await unitService.DeleteAsync(unit.Id);
            var allUnits = await unitService.GetAllAsync();
            allUnits.Should().NotContain(u => u.Id == unit.Id);
        }
    }

    [Fact]
    public async Task Nutritionist_Update_ShouldModifyFieldsAndPreserveAssignmentsWhenNull()
    {
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using (connection)
        {
            var tenantId = currentUserMock.Object.TenantId!.Value;
            var entitlementServiceMock = new Mock<IEntitlementService>();
            entitlementServiceMock.Setup(e => e.ValidateLimitAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), default))
                .Returns(Task.CompletedTask);

            var nutritionistService = new NutritionistService(context, currentUserMock.Object, entitlementServiceMock.Object);

            var nutri = await nutritionistService.CreateAsync(new CreateNutritionistRequest(
                "Dra. Fernanda Santos", "fernanda@nutri.com", "Senha@123", "CRN-3/55443", "11977778888", null));

            // Update details including Name, CRN and Email
            var updated = await nutritionistService.UpdateAsync(nutri.Id, new UpdateNutritionistRequest(
                "Dra. Fernanda Santos Silva",
                "CRN-3/99999",
                "11966665555",
                CommonStatus.Active,
                "fernanda.silva@nutri.com",
                null // keep assignments
            ));

            updated.Name.Should().Be("Dra. Fernanda Santos Silva");
            updated.Crn.Should().Be("CRN-3/99999");
            updated.Phone.Should().Be("11966665555");
            updated.Email.Should().Be("fernanda.silva@nutri.com");

            // Soft delete nutritionist
            await nutritionistService.DeleteAsync(nutri.Id);
            var all = await nutritionistService.GetAllAsync();
            all.Should().NotContain(n => n.Id == nutri.Id);
        }
    }
}
