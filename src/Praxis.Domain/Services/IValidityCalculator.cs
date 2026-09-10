using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Services;

public interface IValidityCalculator
{
    /// <summary>
    /// Seleciona a regra mais específica disponível segundo a hierarquia de prioridades:
    /// 1. Produto específico + Unidade específica
    /// 2. Produto específico global (Tenant)
    /// 3. Categoria de produto + Unidade específica
    /// 4. Categoria de produto global (Tenant)
    /// 5. Regra geral da Unidade
    /// 6. Regra geral do Tenant
    /// </summary>
    ValidityRule? ResolveRule(
        Guid tenantId,
        Guid unitId,
        Guid productId,
        string? productCategory,
        LabelType labelType,
        LabelOperationType operationType,
        StorageCondition storageCondition,
        decimal? temperature,
        IEnumerable<ValidityRule> activeRules);

    /// <summary>
    /// Calcula a validade a partir do início da operação e da regra (ou entrada manual).
    /// </summary>
    ValidityCalculationResult Calculate(
        DateTime startAt,
        ValidityRule? matchedRule,
        DateTime? manualExpiration = null,
        string? manualJustification = null,
        DateTime? originalExpirationDate = null);
}
