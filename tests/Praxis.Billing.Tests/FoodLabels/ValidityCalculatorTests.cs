using FluentAssertions;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using Praxis.Domain.Services;
using Xunit;

namespace Praxis.Billing.Tests.FoodLabels;

public class ValidityCalculatorTests
{
    private readonly ValidityCalculator _calculator = new();

    [Fact]
    public void Calculate_With5DaysRule_ShouldCalculateExactFutureDateTime()
    {
        // Arrange
        var startAt = new DateTime(2026, 9, 10, 8, 30, 0, DateTimeKind.Utc);
        var rule = new ValidityRule
        {
            Name = "Alimentos preparados refrigerados",
            ValidityValue = 5,
            ValidityUnit = ValidityUnit.Days,
            TechnicalBasis = "RDC 216/2004",
            RegulatoryReference = "RDC 216/2004 Art. 4.8.15"
        };

        // Act
        var result = _calculator.Calculate(startAt, rule);

        // Assert
        result.Should().NotBeNull();
        result.ExpirationDate.Should().Be(new DateTime(2026, 9, 15, 8, 30, 0, DateTimeKind.Utc));
        result.Source.Should().Be(ValiditySource.Rule);
        result.RuleMatched.Should().BeTrue();
        result.IsManual.Should().BeFalse();
    }

    [Fact]
    public void Calculate_WithHoursRule_ShouldPreserveExactHour()
    {
        // Arrange
        var startAt = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc);
        var rule = new ValidityRule
        {
            Name = "Sobras quentes em espera",
            ValidityValue = 12,
            ValidityUnit = ValidityUnit.Hours
        };

        // Act
        var result = _calculator.Calculate(startAt, rule);

        // Assert
        result.ExpirationDate.Should().Be(new DateTime(2026, 9, 10, 20, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Calculate_WithWeeksRule_ShouldAddExactDays()
    {
        // Arrange
        var startAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var rule = new ValidityRule
        {
            Name = "Molhos congelados",
            ValidityValue = 2,
            ValidityUnit = ValidityUnit.Weeks
        };

        // Act
        var result = _calculator.Calculate(startAt, rule);

        // Assert
        result.ExpirationDate.Should().Be(new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Calculate_WithMonthRollover_ShouldHandleCorrectly()
    {
        // Arrange
        var startAt = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc);
        var rule = new ValidityRule
        {
            Name = "Insumo seco selado",
            ValidityValue = 1,
            ValidityUnit = ValidityUnit.Months
        };

        // Act
        var result = _calculator.Calculate(startAt, rule);

        // Assert (2026 não é bissexto -> 28 de fevereiro)
        result.ExpirationDate.Should().Be(new DateTime(2026, 2, 28, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Calculate_WhenRuleExceedsOriginalManufacturerExpiration_ShouldCapAtOriginalExpiration()
    {
        // Arrange: Alimento aberto em 10/09 com regra de 5 dias (daria 15/09),
        // porém a data original de validade do lote da embalagem é 12/09!
        var startAt = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc);
        var originalExp = new DateTime(2026, 9, 12, 23, 59, 0, DateTimeKind.Utc);

        var rule = new ValidityRule
        {
            Name = "Regra 5 dias",
            ValidityValue = 5,
            ValidityUnit = ValidityUnit.Days
        };

        // Act
        var result = _calculator.Calculate(startAt, rule, originalExpirationDate: originalExp);

        // Assert: Não pode passar da data do fabricante!
        result.ExpirationDate.Should().Be(originalExp);
        result.Explanation.Should().Contain("prazo original");
    }

    [Fact]
    public void Calculate_WithManualDate_ShouldReturnManualSourceAndJustification()
    {
        // Arrange
        var startAt = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc);
        var manualDate = new DateTime(2026, 9, 13, 18, 0, 0, DateTimeKind.Utc);
        var justification = "Determinado por RT com base em laudo microbiológico";

        // Act
        var result = _calculator.Calculate(startAt, null, manualExpiration: manualDate, manualJustification: justification);

        // Assert
        result.ExpirationDate.Should().Be(manualDate);
        result.Source.Should().Be(ValiditySource.Manual);
        result.IsManual.Should().BeTrue();
        result.TechnicalBasis.Should().Be(justification);
    }

    [Fact]
    public void ResolveRule_ShouldRespectHierarchy_ProductSpecificOverCategoryAndTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        string category = "Carnes";

        var generalTenantRule = new ValidityRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Regra Geral Tenant (3 dias)",
            ValidityValue = 3,
            ValidityUnit = ValidityUnit.Days,
            IsActive = true
        };

        var categoryRule = new ValidityRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductCategory = category,
            Name = "Regra Carnes (4 dias)",
            ValidityValue = 4,
            ValidityUnit = ValidityUnit.Days,
            IsActive = true
        };

        var productSpecificRule = new ValidityRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            UnitId = unitId,
            Name = "Regra Específica Frango Desfiado (5 dias)",
            ValidityValue = 5,
            ValidityUnit = ValidityUnit.Days,
            IsActive = true
        };

        var rules = new List<ValidityRule> { generalTenantRule, categoryRule, productSpecificRule };

        // Act
        var matched = _calculator.ResolveRule(
            tenantId,
            unitId,
            productId,
            category,
            LabelType.PreparedFood,
            LabelOperationType.Preparation,
            StorageCondition.Refrigerated,
            temperature: 4,
            rules
        );

        // Assert: A regra mais específica do produto deve vencer
        matched.Should().NotBeNull();
        matched!.Id.Should().Be(productSpecificRule.Id);
        matched.ValidityValue.Should().Be(5);
    }
}
