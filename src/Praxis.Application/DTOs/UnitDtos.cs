using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record AssignedNutritionistDto(
    Guid NutritionistId,
    Guid UserId,
    string Name,
    string Email,
    string Crn,
    string Phone,
    CommonStatus Status
);

public record UnitDto(
    Guid Id,
    Guid ClientCompanyId,
    string ClientCompanyName,
    string Name,
    string Address,
    string Phone,
    string ResponsibleName,
    string? Notes,
    CommonStatus Status,
    DateTime CreatedAt,
    string? ActiveArtNumber,
    int TotalVisits,
    List<AssignedNutritionistDto>? AssignedNutritionists = null
);

public record CreateUnitRequest(
    Guid ClientCompanyId,
    string Name,
    string Address,
    string Phone,
    string ResponsibleName,
    string? Notes
);

public record UpdateUnitRequest(
    string Name,
    string Address,
    string Phone,
    string ResponsibleName,
    string? Notes,
    CommonStatus Status
);
