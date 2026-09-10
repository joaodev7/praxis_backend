using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record NutritionistDto(
    Guid Id,
    Guid UserId,
    string Name,
    string Email,
    string Crn,
    string Phone,
    CommonStatus Status,
    DateTime CreatedAt,
    List<Guid> AssignedUnitIds
);

public record CreateNutritionistRequest(
    string Name,
    string Email,
    string Password,
    string Crn,
    string Phone,
    List<Guid>? AssignedUnitIds
);

public record UpdateNutritionistRequest(
    string Name,
    string Crn,
    string Phone,
    CommonStatus Status,
    string? Email = null,
    List<Guid>? AssignedUnitIds = null
);
