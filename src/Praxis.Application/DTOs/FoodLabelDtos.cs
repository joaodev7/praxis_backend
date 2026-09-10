using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record FoodLabelDto(
    Guid Id,
    Guid TenantId,
    Guid UnitId,
    string UnitName,
    Guid ProductId,
    string ProductName,
    Guid? ProductBatchId,
    string? ProductBatchCode,
    Guid? ValidityRuleId,
    string? ValidityRuleName,
    LabelType LabelType,
    LabelOperationType OperationType,
    LabelStatus Status,
    string Description,
    string InternalBatchCode,
    DateTime? ManufacturedAt,
    DateTime? PreparedAt,
    DateTime? OpenedAt,
    DateTime? PortionedAt,
    DateTime ValidityStartAt,
    DateTime CalculatedExpirationDate,
    DateTime? ManualExpirationDate,
    DateTime EffectiveExpirationDate,
    bool IsExpired,
    ValiditySource ValiditySource,
    string? ValidityJustification,
    StorageCondition StorageCondition,
    decimal? StorageTemperatureMin,
    decimal? StorageTemperatureMax,
    string? StorageInstructions,
    string PublicToken,
    int PrintCount,
    DateTime? LastPrintedAt,
    Guid CreatedByUserId,
    string CreatedByUserName,
    DateTime CreatedAt,
    DateTime? CancelledAt,
    string? CancellationReason,
    DateTime? DiscardedAt,
    string? DiscardReason,
    decimal? DiscardQuantity,
    string? DiscardUnit
);

public record FoodLabelListDto(
    Guid Id,
    Guid UnitId,
    string UnitName,
    Guid ProductId,
    string ProductName,
    string InternalBatchCode,
    LabelType LabelType,
    LabelOperationType OperationType,
    LabelStatus Status,
    DateTime ValidityStartAt,
    DateTime EffectiveExpirationDate,
    bool IsExpired,
    StorageCondition StorageCondition,
    decimal? StorageTemperatureMax,
    string PublicToken,
    int PrintCount,
    DateTime CreatedAt
);

public record CreateFoodLabelRequest(
    Guid UnitId,
    Guid ProductId,
    Guid? ProductBatchId,
    LabelType LabelType,
    LabelOperationType OperationType,
    string? Description,
    DateTime? OperationDateTime,
    StorageCondition StorageCondition,
    decimal? StorageTemperatureMin,
    decimal? StorageTemperatureMax,
    string? StorageInstructions,
    DateTime? ManualExpirationDate,
    string? ManualJustification,
    int PrintCopies = 1,
    string? TemplateType = null
);

public record CancelFoodLabelRequest(
    string Reason
);

public record DiscardFoodLabelRequest(
    string Reason,
    decimal? Quantity,
    string? Unit
);

public record ReprintFoodLabelRequest(
    int Quantity = 1,
    string? PrinterName = null,
    string? TemplateUsed = null
);

public record FoodLabelDashboardDto(
    int TotalActive,
    int ExpiresToday,
    int ExpiresTomorrow,
    int Expired,
    int Cancelled,
    int Discarded,
    int TotalPrinted,
    int WithoutConfiguredRule
);

public record FoodLabelFilterParams(
    Guid? UnitId,
    Guid? ProductId,
    LabelStatus? Status,
    LabelType? LabelType,
    LabelOperationType? OperationType,
    string? BatchCode,
    DateTime? ExpirationFrom,
    DateTime? ExpirationTo,
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    bool? OnlyExpired,
    bool? OnlyExpiringSoon
);

public record FoodLabelPublicDto(
    string ProductName,
    string InternalBatchCode,
    string? OperationName,
    DateTime ValidityStartAt,
    DateTime ExpirationDate,
    bool IsExpired,
    string StorageCondition,
    decimal? StorageTemperatureMax,
    string? StorageInstructions,
    string UnitName,
    string Status,
    string? TechnicalBasis
);

public record LabelTemplateDto(
    Guid Id,
    string Name,
    string TemplateType,
    decimal WidthMm,
    decimal HeightMm,
    bool IncludeQrCode,
    bool IncludeLogo,
    bool IsDefault
);

public record PrintLabelsRequest(
    List<Guid> LabelIds,
    string TemplateType = "Thermal80x50",
    int CopiesPerLabel = 1
);
