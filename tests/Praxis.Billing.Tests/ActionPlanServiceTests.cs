using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Application.Services;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using Xunit;

namespace Praxis.Billing.Tests;

public class ActionPlanServiceTests
{
    private readonly Mock<IFileStorageService> _storageMock;
    private readonly Mock<ILogger<ActionPlanService>> _loggerMock;

    public ActionPlanServiceTests()
    {
        _storageMock = new Mock<IFileStorageService>();
        _loggerMock = new Mock<ILogger<ActionPlanService>>();
    }

    private async Task<(Guid clientId, Guid unitId, Guid visitId, Guid ncId)> SeedBasicStructureAsync(
        Praxis.Infrastructure.Data.ApplicationDbContext context,
        Guid tenantId)
    {
        var client = new ClientCompany
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LegalName = "Restaurante Central Ltda",
            TradeName = "Restaurante Central",
            Cnpj = "12.345.678/0001-99"
        };
        context.ClientCompanies.Add(client);

        var unit = new Unit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClientCompanyId = client.Id,
            Name = "Unidade Matriz",
            Address = "Rua das Flores, 123"
        };
        context.Units.Add(unit);

        var user = await context.Users.FirstAsync(u => u.TenantId == tenantId);

        var nutri = new Nutritionist
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            Crn = "CRN-1234",
            Phone = "(11) 99999-0000"
        };
        context.Nutritionists.Add(nutri);

        var visit = new Visit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UnitId = unit.Id,
            NutritionistId = nutri.Id,
            ScheduledAt = DateTime.UtcNow,
            Status = VisitStatus.InProgress
        };
        context.Visits.Add(visit);

        var nc = new NonConformity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VisitId = visit.Id,
            Category = "Higienização",
            Description = "Bancada com acúmulo de sujidades",
            Severity = NonConformitySeverity.Alta,
            Status = NonConformityStatus.Aberta
        };
        context.NonConformities.Add(nc);

        await context.SaveChangesAsync();

        return (client.Id, unit.Id, visit.Id, nc.Id);
    }

    [Fact]
    public async Task CreateAsync_WithValid5W2HData_ShouldCreateActionPlanSuccessfully()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);

        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var request = new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Higienizar bancada central",
            Why: "Eliminar risco de contaminação cruzada",
            ResponsibleUserId: null,
            ResponsibleName: "João Silva",
            DueDate: DateTime.UtcNow.AddDays(3),
            Where: "Cozinha principal",
            How: "Utilizar detergente desengordurante e sanitizante clorado",
            HowMuch: 50.00m,
            Priority: ActionPlanPriority.Alta,
            Notes: "Prioridade para o turno da manhã"
        );

        var result = await service.CreateAsync(request);

        result.Should().NotBeNull();
        result.NonConformityId.Should().Be(ncId);
        result.What.Should().Be("Higienizar bancada central");
        result.Why.Should().Be("Eliminar risco de contaminação cruzada");
        result.ResponsibleName.Should().Be("João Silva");
        result.Where.Should().Be("Cozinha principal");
        result.How.Should().Be("Utilizar detergente desengordurante e sanitizante clorado");
        result.HowMuch.Should().Be(50.00m);
        result.Priority.Should().Be(ActionPlanPriority.Alta);
        result.Status.Should().Be(ActionItemStatus.Pendente);
        result.IsLate.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_MissingWhat_ShouldThrowArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var request = new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "   ",
            Why: "Razão",
            ResponsibleUserId: null,
            ResponsibleName: "João",
            DueDate: DateTime.UtcNow.AddDays(3),
            Where: "Cozinha",
            How: "Como",
            HowMuch: null
        );

        var act = async () => await service.CreateAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*What*");
    }

    [Fact]
    public async Task CreateAsync_MissingDueDate_ShouldThrowArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var request = new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Fazer limpeza",
            Why: "Razão",
            ResponsibleUserId: null,
            ResponsibleName: "João",
            DueDate: default,
            Where: "Cozinha",
            How: "Como",
            HowMuch: null
        );

        var act = async () => await service.CreateAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*prazo*");
    }

    [Fact]
    public async Task ChangeStatusAsync_ToInProgress_WithoutResponsible_ShouldThrowInvalidOperationException()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var plan = await service.CreateAsync(new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Limpar câmara fria",
            Why: null,
            ResponsibleUserId: null,
            ResponsibleName: null,
            DueDate: DateTime.UtcNow.AddDays(5),
            Where: null,
            How: null,
            HowMuch: null
        ));

        // RN03: Não é permitido colocar uma ação em execução sem responsável
        var act = async () => await service.ChangeStatusAsync(plan.Id, new ChangeActionPlanStatusRequest(ActionItemStatus.EmAndamento, null));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sem responsável definido*");
    }

    [Fact]
    public async Task ChangeStatusAsync_ToInProgress_WithResponsible_ShouldSucceedAndSetStartedAt()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var plan = await service.CreateAsync(new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Limpar câmara fria",
            Why: null,
            ResponsibleUserId: null,
            ResponsibleName: "Maria Cozinheira",
            DueDate: DateTime.UtcNow.AddDays(5),
            Where: null,
            How: null,
            HowMuch: null
        ));

        var updated = await service.ChangeStatusAsync(plan.Id, new ChangeActionPlanStatusRequest(ActionItemStatus.EmAndamento, "Iniciado"));

        updated.Status.Should().Be(ActionItemStatus.EmAndamento);
        updated.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ChangeStatusAsync_ToWaitingValidation_ShouldSucceedAndSetCompletedAt()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var plan = await service.CreateAsync(new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Higienizar utensílios",
            Why: null,
            ResponsibleUserId: null,
            ResponsibleName: "Carlos",
            DueDate: DateTime.UtcNow.AddDays(2),
            Where: null,
            How: null,
            HowMuch: null
        ));

        var updated = await service.ChangeStatusAsync(plan.Id, new ChangeActionPlanStatusRequest(ActionItemStatus.AguardandoValidacao, "Execução finalizada"));

        updated.Status.Should().Be(ActionItemStatus.AguardandoValidacao);
        updated.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateAsync_Approved_ByNutritionist_ShouldConcludeActionPlan()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        currentUserMock.Setup(c => c.Role).Returns(UserRole.Nutritionist);

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var plan = await service.CreateAsync(new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Organizar estoque",
            Why: null,
            ResponsibleUserId: null,
            ResponsibleName: "Carlos",
            DueDate: DateTime.UtcNow.AddDays(2),
            Where: null,
            How: null,
            HowMuch: null
        ));

        await service.ChangeStatusAsync(plan.Id, new ChangeActionPlanStatusRequest(ActionItemStatus.AguardandoValidacao, null));

        var validated = await service.ValidateAsync(plan.Id, new ValidateActionPlanRequest(true, "Excelente trabalho, tudo conforme"));

        validated.Status.Should().Be(ActionItemStatus.Concluida);
        validated.ValidatedAt.Should().NotBeNull();
        validated.ValidatedByUserId.Should().Be(currentUserMock.Object.UserId);
        validated.ValidationComment.Should().Be("Excelente trabalho, tudo conforme");
    }

    [Fact]
    public async Task ValidateAsync_Rejected_ByNutritionist_WithComment_ShouldReopenActionPlan()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        currentUserMock.Setup(c => c.Role).Returns(UserRole.Nutritionist);

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var plan = await service.CreateAsync(new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Organizar estoque",
            Why: null,
            ResponsibleUserId: null,
            ResponsibleName: "Carlos",
            DueDate: DateTime.UtcNow.AddDays(2),
            Where: null,
            How: null,
            HowMuch: null
        ));

        await service.ChangeStatusAsync(plan.Id, new ChangeActionPlanStatusRequest(ActionItemStatus.AguardandoValidacao, null));

        var reopened = await service.ValidateAsync(plan.Id, new ValidateActionPlanRequest(false, "Falta higienizar a prateleira inferior"));

        reopened.Status.Should().Be(ActionItemStatus.EmAndamento);
        reopened.ValidationComment.Should().Be("Falta higienizar a prateleira inferior");
        reopened.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_Rejected_WithoutComment_ShouldThrowArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        currentUserMock.Setup(c => c.Role).Returns(UserRole.Nutritionist);

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var plan = await service.CreateAsync(new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Organizar estoque",
            Why: null,
            ResponsibleUserId: null,
            ResponsibleName: "Carlos",
            DueDate: DateTime.UtcNow.AddDays(2),
            Where: null,
            How: null,
            HowMuch: null
        ));

        var act = async () => await service.ValidateAsync(plan.Id, new ValidateActionPlanRequest(false, "   "));
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*justificativa*");
    }

    [Fact]
    public async Task ValidateAsync_ByClientUser_ShouldThrowUnauthorizedAccessException()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        currentUserMock.Setup(c => c.Role).Returns(UserRole.ClientUser);

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var plan = await service.CreateAsync(new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Organizar estoque",
            Why: null,
            ResponsibleUserId: null,
            ResponsibleName: "Carlos",
            DueDate: DateTime.UtcNow.AddDays(2),
            Where: null,
            How: null,
            HowMuch: null
        ));

        var act = async () => await service.ValidateAsync(plan.Id, new ValidateActionPlanRequest(true, "Aprovado"));
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*permissão*");
    }

    [Fact]
    public async Task CancelAsync_WithReason_ByAdmin_ShouldCancelActionPlan()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        currentUserMock.Setup(c => c.Role).Returns(UserRole.TenantAdmin);

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var plan = await service.CreateAsync(new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Trocar equipamento",
            Why: null,
            ResponsibleUserId: null,
            ResponsibleName: "Equipe de Manutenção",
            DueDate: DateTime.UtcNow.AddDays(10),
            Where: null,
            How: null,
            HowMuch: null
        ));

        var cancelled = await service.CancelAsync(plan.Id, new CancelActionPlanRequest("Equipamento foi substituído por outro fornecedor."));

        cancelled.Status.Should().Be(ActionItemStatus.Cancelada);
        cancelled.CancellationReason.Should().Be("Equipamento foi substituído por outro fornecedor.");
    }

    [Fact]
    public async Task CancelAsync_WithoutReason_ShouldThrowArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        currentUserMock.Setup(c => c.Role).Returns(UserRole.TenantAdmin);

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var plan = await service.CreateAsync(new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Trocar equipamento",
            Why: null,
            ResponsibleUserId: null,
            ResponsibleName: "Equipe",
            DueDate: DateTime.UtcNow.AddDays(10),
            Where: null,
            How: null,
            HowMuch: null
        ));

        var act = async () => await service.CancelAsync(plan.Id, new CancelActionPlanRequest("  "));
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*motivo*");
    }

    [Fact]
    public async Task LateCalculation_WhenDueDatePastAndNotCompleted_ShouldReturnIsLateTrue()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var plan = await service.CreateAsync(new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Ação atrasada",
            Why: null,
            ResponsibleUserId: null,
            ResponsibleName: "Responsável",
            DueDate: DateTime.UtcNow.AddDays(-2), // No passado
            Where: null,
            How: null,
            HowMuch: null
        ));

        var item = await service.GetByIdAsync(plan.Id);
        item.IsLate.Should().BeTrue();
    }

    [Fact]
    public async Task AddEvidenceAsync_ValidFile_ShouldUploadToR2AndSaveEvidenceRecord()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var plan = await service.CreateAsync(new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Higienização completa",
            Why: null,
            ResponsibleUserId: null,
            ResponsibleName: "Maria",
            DueDate: DateTime.UtcNow.AddDays(1),
            Where: null,
            How: null,
            HowMuch: null
        ));

        var fakeStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 });
        var fileName = "bancada_limpa.jpg";
        var contentType = "image/jpeg";

        _storageMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), contentType, It.IsAny<CancellationToken>()))
            .ReturnsAsync("objectKey");

        _storageMock
            .Setup(s => s.GetFileUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://r2.praxis.dev/bancada_limpa.jpg");

        var evidence = await service.AddEvidenceAsync(plan.Id, fakeStream, fileName, contentType, fakeStream.Length);

        evidence.Should().NotBeNull();
        evidence.ActionPlanId.Should().Be(plan.Id);
        evidence.FileName.Should().Be(fileName);
        evidence.ContentType.Should().Be(contentType);
        evidence.ObjectKey.Should().StartWith($"tenants/{tenantId}/action-plans/{plan.Id}/evidences/");
        evidence.FileUrl.Should().Be("https://r2.praxis.dev/bancada_limpa.jpg");

        var list = await service.GetEvidencesAsync(plan.Id);
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddEvidenceAsync_InvalidExtension_ShouldThrowArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var plan = await service.CreateAsync(new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Ação",
            Why: null,
            ResponsibleUserId: null,
            ResponsibleName: "Maria",
            DueDate: DateTime.UtcNow.AddDays(1),
            Where: null,
            How: null,
            HowMuch: null
        ));

        var fakeStream = new MemoryStream(new byte[] { 0x4D, 0x5A });
        var act = async () => await service.AddEvidenceAsync(plan.Id, fakeStream, "script.exe", "application/x-msdownload", fakeStream.Length);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Extensão*não permitida*");
    }

    [Fact]
    public async Task DeleteEvidenceAsync_ExistingEvidence_ShouldSoftDeleteAndCallR2Delete()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var plan = await service.CreateAsync(new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Higienização",
            Why: null,
            ResponsibleUserId: null,
            ResponsibleName: "Maria",
            DueDate: DateTime.UtcNow.AddDays(1),
            Where: null,
            How: null,
            HowMuch: null
        ));

        var fakeStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
        _storageMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("key123");

        _storageMock
            .Setup(s => s.GetFileUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://r2.praxis.dev/foto.jpg");

        var evidence = await service.AddEvidenceAsync(plan.Id, fakeStream, "foto.jpg", "image/jpeg", fakeStream.Length);

        var deleted = await service.DeleteEvidenceAsync(evidence.Id);
        deleted.Should().BeTrue();

        _storageMock.Verify(s => s.DeleteAsync(evidence.ObjectKey, It.IsAny<CancellationToken>()), Times.Once);

        var dbEvidence = await context.ActionPlanEvidences.FindAsync(evidence.Id);
        dbEvidence!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task TenantIsolation_CannotAccessActionPlanFromAnotherTenant()
    {
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenant1Id);
        using var conn = connection;

        var (_, _, _, ncId) = await SeedBasicStructureAsync(context, tenant1Id);
        var serviceTenant1 = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var plan = await serviceTenant1.CreateAsync(new CreateActionPlanRequest(
            NonConformityId: ncId,
            What: "Ação Privada do Tenant 1",
            Why: null,
            ResponsibleUserId: null,
            ResponsibleName: "Resp",
            DueDate: DateTime.UtcNow.AddDays(1),
            Where: null,
            How: null,
            HowMuch: null
        ));

        // Switch to Tenant 2
        var currentUserTenant2Mock = new Mock<ICurrentUserService>();
        currentUserTenant2Mock.Setup(c => c.TenantId).Returns(tenant2Id);
        currentUserTenant2Mock.Setup(c => c.UserId).Returns(Guid.NewGuid());
        currentUserTenant2Mock.Setup(c => c.Role).Returns(UserRole.TenantAdmin);

        var serviceTenant2 = new ActionPlanService(context, currentUserTenant2Mock.Object, _storageMock.Object, _loggerMock.Object);

        var act = async () => await serviceTenant2.GetByIdAsync(plan.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>();

        var allTenant2 = await serviceTenant2.GetAllAsync();
        allTenant2.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboardAsync_ShouldReturnAccurateCountsAndClientMetrics()
    {
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var (clientId, _, _, ncId) = await SeedBasicStructureAsync(context, tenantId);
        var service = new ActionPlanService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        // Plan 1: Pending
        await service.CreateAsync(new CreateActionPlanRequest(ncId, "Ação 1", null, null, "Resp 1", DateTime.UtcNow.AddDays(5), null, null, null));

        // Plan 2: In progress
        var p2 = await service.CreateAsync(new CreateActionPlanRequest(ncId, "Ação 2", null, null, "Resp 2", DateTime.UtcNow.AddDays(5), null, null, null));
        await service.ChangeStatusAsync(p2.Id, new ChangeActionPlanStatusRequest(ActionItemStatus.EmAndamento, null));

        // Plan 3: Completed
        var p3 = await service.CreateAsync(new CreateActionPlanRequest(ncId, "Ação 3", null, null, "Resp 3", DateTime.UtcNow.AddDays(5), null, null, null));
        await service.ValidateAsync(p3.Id, new ValidateActionPlanRequest(true, "Aprovado"));

        // Plan 4: Late
        await service.CreateAsync(new CreateActionPlanRequest(ncId, "Ação 4 Atrasada", null, null, "Resp 4", DateTime.UtcNow.AddDays(-2), null, null, null));

        var dashboard = await service.GetDashboardAsync();

        dashboard.TotalActions.Should().Be(4);
        dashboard.PendingActions.Should().Be(2); // p1 and p4 are Pending
        dashboard.InProgressActions.Should().Be(1);
        dashboard.CompletedActions.Should().Be(1);
        dashboard.LateActions.Should().Be(1);

        dashboard.ClientMetrics.Should().HaveCount(1);
        dashboard.ClientMetrics[0].ClientCompanyId.Should().Be(clientId);
        dashboard.ClientMetrics[0].TotalActions.Should().Be(4);
        dashboard.ClientMetrics[0].CompletedActions.Should().Be(1);
        dashboard.ClientMetrics[0].LateActions.Should().Be(1);
    }
}
