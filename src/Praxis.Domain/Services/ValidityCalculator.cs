using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Services;

public class ValidityCalculator : IValidityCalculator
{
    public ValidityRule? ResolveRule(
        Guid tenantId,
        Guid unitId,
        Guid productId,
        string? productCategory,
        LabelType labelType,
        LabelOperationType operationType,
        StorageCondition storageCondition,
        decimal? temperature,
        IEnumerable<ValidityRule> activeRules)
    {
        var candidates = activeRules
            .Where(r => r.IsActive && !r.IsDeleted && r.TenantId == tenantId)
            .Where(r => r.OperationType == null || r.OperationType == operationType)
            .Where(r => r.LabelType == null || r.LabelType == labelType)
            .Where(r => r.StorageCondition == null || r.StorageCondition == storageCondition)
            .Where(r => !temperature.HasValue || !r.MaximumTemperature.HasValue || temperature.Value <= r.MaximumTemperature.Value)
            .ToList();

        if (!candidates.Any())
            return null;

        return candidates
            .Select(r => new { Rule = r, Priority = GetPriorityScore(r, unitId, productId, productCategory) })
            .Where(x => x.Priority > 0)
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.Rule.MaximumTemperature.HasValue) // Mais restritiva em temperatura
            .Select(x => x.Rule)
            .FirstOrDefault();
    }

    private static int GetPriorityScore(ValidityRule rule, Guid unitId, Guid productId, string? productCategory)
    {
        bool matchesProduct = rule.ProductId.HasValue && rule.ProductId.Value == productId;
        bool matchesCategory = !string.IsNullOrWhiteSpace(rule.ProductCategory) &&
                               !string.IsNullOrWhiteSpace(productCategory) &&
                               string.Equals(rule.ProductCategory.Trim(), productCategory.Trim(), StringComparison.OrdinalIgnoreCase);
        bool matchesUnit = rule.UnitId.HasValue && rule.UnitId.Value == unitId;
        bool isGlobalUnit = !rule.UnitId.HasValue;

        // Se a regra tem UnitId diferente da unidade atual, descarta
        if (rule.UnitId.HasValue && rule.UnitId.Value != unitId)
            return 0;

        // 1. Produto específico + Unidade específica
        if (matchesProduct && matchesUnit) return 60;

        // 2. Produto específico global (Tenant)
        if (matchesProduct && isGlobalUnit) return 50;

        // 3. Categoria de produto + Unidade específica
        if (matchesCategory && matchesUnit) return 40;

        // 4. Categoria de produto global (Tenant)
        if (matchesCategory && isGlobalUnit) return 30;

        // 5. Regra geral da Unidade (sem produto e sem categoria)
        if (!rule.ProductId.HasValue && string.IsNullOrWhiteSpace(rule.ProductCategory) && matchesUnit) return 20;

        // 6. Regra geral do Tenant (sem produto e sem categoria)
        if (!rule.ProductId.HasValue && string.IsNullOrWhiteSpace(rule.ProductCategory) && isGlobalUnit) return 10;

        return 0;
    }

    public ValidityCalculationResult Calculate(
        DateTime startAt,
        ValidityRule? matchedRule,
        DateTime? manualExpiration = null,
        string? manualJustification = null,
        DateTime? originalExpirationDate = null)
    {
        // 1. Caso validade manual informada
        if (manualExpiration.HasValue)
        {
            var finalManual = manualExpiration.Value;
            string? explanation = "Validade informada manualmente pelo operador.";

            if (originalExpirationDate.HasValue && finalManual > originalExpirationDate.Value)
            {
                finalManual = originalExpirationDate.Value;
                explanation = "Validade manual ajustada para não ultrapassar a validade original do fabricante.";
            }

            return new ValidityCalculationResult
            {
                StartAt = startAt,
                ExpirationDate = finalManual,
                Source = ValiditySource.Manual,
                RuleId = matchedRule?.Id,
                Explanation = explanation,
                TechnicalBasis = manualJustification,
                IsManual = true,
                RuleMatched = matchedRule != null
            };
        }

        // 2. Sem regra e sem data manual
        if (matchedRule == null)
        {
            return new ValidityCalculationResult
            {
                StartAt = startAt,
                ExpirationDate = startAt,
                Source = ValiditySource.Manual,
                RuleId = null,
                Explanation = "Nenhuma regra de validade aplicável encontrada. Necessário informar validade manual com justificativa técnica.",
                IsManual = true,
                RuleMatched = false
            };
        }

        // 3. Cálculo automático pela regra
        DateTime calculated = matchedRule.ValidityUnit switch
        {
            ValidityUnit.Hours => startAt.AddHours(matchedRule.ValidityValue),
            ValidityUnit.Days => startAt.AddDays(matchedRule.ValidityValue),
            ValidityUnit.Weeks => startAt.AddDays(matchedRule.ValidityValue * 7),
            ValidityUnit.Months => startAt.AddMonths(matchedRule.ValidityValue),
            _ => startAt.AddDays(matchedRule.ValidityValue)
        };

        string explanationText = $"Calculado pela regra '{matchedRule.Name}' ({matchedRule.ValidityValue} {matchedRule.ValidityUnit}).";

        // Regra Sanitária Crítica: Não ultrapassar a validade original do produto/lote de origem
        if (originalExpirationDate.HasValue && calculated > originalExpirationDate.Value)
        {
            calculated = originalExpirationDate.Value;
            explanationText += " [Ajustado para não ultrapassar o prazo original do fabricante]";
        }

        return new ValidityCalculationResult
        {
            StartAt = startAt,
            ExpirationDate = calculated,
            Source = ValiditySource.Rule,
            RuleId = matchedRule.Id,
            Explanation = explanationText,
            TechnicalBasis = matchedRule.TechnicalBasis,
            RegulatoryReference = matchedRule.RegulatoryReference,
            IsManual = false,
            RuleMatched = true
        };
    }
}
