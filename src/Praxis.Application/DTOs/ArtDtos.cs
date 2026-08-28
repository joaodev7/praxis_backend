using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record ArtDto(
    Guid Id,
    Guid UnitId,
    string UnitName,
    string ClientCompanyName,
    Guid NutritionistId,
    string NutritionistName,
    string Number,
    DateTime StartDate,
    DateTime? EndDate,
    ArtStatus Status,
    string? DocumentUrl,
    string? Notes,
    DateTime CreatedAt
);

public record CreateArtRequest(
    Guid UnitId,
    Guid NutritionistId,
    string Number,
    DateTime StartDate,
    DateTime? EndDate,
    string? DocumentUrl,
    string? Notes
);

public record UpdateArtRequest(
    string Number,
    DateTime StartDate,
    DateTime? EndDate,
    ArtStatus Status,
    string? DocumentUrl,
    string? Notes
);
