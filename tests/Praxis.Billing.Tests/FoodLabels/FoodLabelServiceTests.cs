using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Application.Services;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using Praxis.Domain.Services;
using Praxis.Infrastructure.Data;
using Xunit;

namespace Praxis.Billing.Tests.FoodLabels;

public class FoodLabelServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly ValidityCalculator _validityCalculator;
    private readonly FoodLabelService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    public FoodLabelServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"FoodLabelsTestDb_{Guid.NewGuid()}")
            .Options;

        _currentUserMock = new Mock<ICurrentUserService>();
        _currentUserMock.Setup(u => u.TenantId).Returns(_tenantId);
        _currentUserMock.Setup(u => u.UserId).Returns(_userId);
        _currentUserMock.Setup(u => u.UserEmail).Returns("rt@praxisnutri.com.br");

        _context = new ApplicationDbContext(options, _currentUserMock.Object);
        _validityCalculator = new ValidityCalculator();
        _service = new FoodLabelService(_context, _currentUserMock.Object, _validityCalculator);

        SeedDatabase();
    }

    private void SeedDatabase()
    {
        var tenant = new Tenant
        {
            Id = _tenantId,
            Name = "Nutri Consultoria",
            Cnpj = "12.345.678/0001-90"
        };
        _context.Tenants.Add(tenant);

        var client = new ClientCompany
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            TradeName = "Restaurante Central",
            LegalName = "Restaurante Central Ltda",
            Cnpj = "98.765.432/0001-10"
        };
        _context.ClientCompanies.Add(client);

        var unit = new Unit
        {
            Id = _unitId,
            TenantId = _tenantId,
            ClientCompanyId = client.Id,
            Name = "Cozinha Central",
            Address = "Rua das Flores, 123"
        };
        _context.Units.Add(unit);

        var user = new User
        {
            Id = _userId,
            TenantId = _tenantId,
            Name = "Dra. RT Nutricionista",
            Email = "rt@praxisnutri.com.br",
            PasswordHash = "hash"
        };
        _context.Users.Add(user);

        var product = new Product
        {
            Id = _productId,
            TenantId = _tenantId,
            Name = "Frango Desfiado Cozido",
            Category = "Aves",
            IsActive = true
        };
        _context.Products.Add(product);

        var rule = new ValidityRule
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Aves Preparadas Refrigeradas",
            ProductCategory = "Aves",
            OperationType = LabelOperationType.Preparation,
            StorageCondition = StorageCondition.Refrigerated,
            MaximumTemperature = 4,
            ValidityValue = 3,
            ValidityUnit = ValidityUnit.Days,
            TechnicalBasis = "RDC 216/2004",
            IsActive = true
        };
        _context.ValidityRules.Add(rule);

        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateAsync_WithMatchedRule_ShouldCalculateValidityAndGenerateBatchCode()
    {
        // Arrange
        var request = new CreateFoodLabelRequest(
            UnitId: _unitId,
            ProductId: _productId,
            ProductBatchId: null,
            LabelType: LabelType.PreparedFood,
            OperationType: LabelOperationType.Preparation,
            Description: "Frango desfiado temperado",
            OperationDateTime: new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc),
            StorageCondition: StorageCondition.Refrigerated,
            StorageTemperatureMin: 0,
            StorageTemperatureMax: 4,
            StorageInstructions: "Manter sob refrigeração ≤ 4°C",
            ManualExpirationDate: null,
            ManualJustification: null,
            PrintCopies: 2
        );

        // Act
        var created = await _service.CreateAsync(request);

        // Assert
        created.Should().NotBeNull();
        created.InternalBatchCode.Should().StartWith("PREP-20260910-");
        created.CalculatedExpirationDate.Should().Be(new DateTime(2026, 9, 13, 8, 0, 0, DateTimeKind.Utc));
        created.EffectiveExpirationDate.Should().Be(new DateTime(2026, 9, 13, 8, 0, 0, DateTimeKind.Utc));
        created.ValiditySource.Should().Be(ValiditySource.Rule);
        created.Status.Should().Be(LabelStatus.Active);
        created.PublicToken.Should().NotBeNullOrWhiteSpace();
        created.PrintCount.Should().Be(2);

        // Verifica auditoria gerada
        var audits = await _context.FoodLabelAudits.Where(a => a.LabelId == created.Id).ToListAsync();
        audits.Should().ContainSingle(a => a.Action == "Created");

        // Verifica prints gerados
        var prints = await _context.LabelPrints.Where(p => p.LabelId == created.Id).ToListAsync();
        prints.Should().ContainSingle(p => p.Quantity == 2);
    }

    [Fact]
    public async Task CancelAsync_ShouldUpdateStatusAndLogAudit()
    {
        // Arrange: Criar etiqueta inicial
        var request = new CreateFoodLabelRequest(
            UnitId: _unitId,
            ProductId: _productId,
            ProductBatchId: null,
            LabelType: LabelType.PreparedFood,
            OperationType: LabelOperationType.Preparation,
            Description: "Frango desfiado",
            OperationDateTime: DateTime.UtcNow,
            StorageCondition: StorageCondition.Refrigerated,
            StorageTemperatureMin: 0,
            StorageTemperatureMax: 4,
            StorageInstructions: null,
            ManualExpirationDate: null,
            ManualJustification: null
        );
        var label = await _service.CreateAsync(request);

        // Act: Cancelar etiqueta
        var cancelReq = new CancelFoodLabelRequest("Lote descartado por falha na refrigeração");
        var cancelled = await _service.CancelAsync(label.Id, cancelReq);

        // Assert
        cancelled.Status.Should().Be(LabelStatus.Cancelled);
        cancelled.CancellationReason.Should().Be("Lote descartado por falha na refrigeração");
        cancelled.CancelledAt.Should().NotBeNull();

        var audits = await _context.FoodLabelAudits.Where(a => a.LabelId == label.Id).ToListAsync();
        audits.Should().Contain(a => a.Action == "Cancelled");
    }

    [Fact]
    public async Task GetPublicByTokenAsync_ShouldReturnLabelDetailsWithoutAuthentication()
    {
        // Arrange
        var request = new CreateFoodLabelRequest(
            UnitId: _unitId,
            ProductId: _productId,
            ProductBatchId: null,
            LabelType: LabelType.PreparedFood,
            OperationType: LabelOperationType.Preparation,
            Description: "Frango desfiado",
            OperationDateTime: new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc),
            StorageCondition: StorageCondition.Refrigerated,
            StorageTemperatureMin: 0,
            StorageTemperatureMax: 4,
            StorageInstructions: "≤ 4°C",
            ManualExpirationDate: null,
            ManualJustification: null
        );
        var label = await _service.CreateAsync(request);

        // Act: Consulta pública (simula fiscal/operador lendo QR code)
        var publicData = await _service.GetPublicByTokenAsync(label.PublicToken);

        // Assert
        publicData.Should().NotBeNull();
        publicData.ProductName.Should().Be("Frango Desfiado Cozido");
        publicData.InternalBatchCode.Should().Be(label.InternalBatchCode);
        publicData.StorageCondition.Should().Be("Refrigerado");
        publicData.UnitName.Should().Be("Cozinha Central");
    }
}
