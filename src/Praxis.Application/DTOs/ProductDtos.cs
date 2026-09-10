namespace Praxis.Application.DTOs;

public record ProductDto(
    Guid Id,
    Guid TenantId,
    Guid? UnitId,
    string? UnitName,
    string Name,
    string? Description,
    string? Category,
    bool IsActive,
    DateTime CreatedAt,
    int BatchCount
);

public record CreateProductRequest(
    Guid? UnitId,
    string Name,
    string? Description,
    string? Category
);

public record UpdateProductRequest(
    string Name,
    string? Description,
    string? Category,
    bool IsActive
);

public record ProductBatchDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string BatchCode,
    string? OriginalBatchCode,
    DateTime? ManufacturingDate,
    DateTime? OriginalExpirationDate,
    DateTime CreatedAt
);

public record CreateProductBatchRequest(
    string BatchCode,
    string? OriginalBatchCode,
    DateTime? ManufacturingDate,
    DateTime? OriginalExpirationDate
);
