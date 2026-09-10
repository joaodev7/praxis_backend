using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record ValidityRuleDto(
    Guid Id,
    Guid TenantId,
    Guid? UnitId,
    string? UnitName,
    Guid? ProductId,
    string? ProductName,
    string Name,
    string? Description,
    string? ProductCategory,
    LabelType? LabelType,
    LabelOperationType? OperationType,
    StorageCondition? StorageCondition,
    decimal? MaximumTemperature,
    int ValidityValue,
    ValidityUnit ValidityUnit,
    bool AllowManualExpiration,
    bool RequiresTechnicalBasis,
    string? TechnicalBasis,
    string? RegulatoryReference,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateValidityRuleRequest(
    Guid? UnitId,
    Guid? ProductId,
    string Name,
    string? Description,
    string? ProductCategory,
    LabelType? LabelType,
    LabelOperationType? OperationType,
    StorageCondition? StorageCondition,
    decimal? MaximumTemperature,
    int ValidityValue,
    ValidityUnit ValidityUnit,
    bool AllowManualExpiration,
    bool RequiresTechnicalBasis,
    string? TechnicalBasis,
    string? RegulatoryReference
);

public record UpdateValidityRuleRequest(
    string Name,
    string? Description,
    string? ProductCategory,
    LabelType? LabelType,
    LabelOperationType? OperationType,
    StorageCondition? StorageCondition,
    decimal? MaximumTemperature,
    int ValidityValue,
    ValidityUnit ValidityUnit,
    bool AllowManualExpiration,
    bool RequiresTechnicalBasis,
    string? TechnicalBasis,
    string? RegulatoryReference,
    bool IsActive
);

public record SimulateValidityRequest(
    Guid UnitId,
    Guid ProductId,
    LabelType LabelType,
    LabelOperationType OperationType,
    StorageCondition StorageCondition,
    decimal? StorageTemperature,
    DateTime? StartAt,
    Guid? ProductBatchId
);

public record SimulateValidityResponse(
    bool RuleMatched,
    Guid? RuleId,
    string? RuleName,
    DateTime StartAt,
    DateTime ExpirationDate,
    ValiditySource Source,
    string? Explanation,
    string? TechnicalBasis,
    string? RegulatoryReference,
    bool RequiresManualInput,
    DateTime? MaxAllowedDate
);
