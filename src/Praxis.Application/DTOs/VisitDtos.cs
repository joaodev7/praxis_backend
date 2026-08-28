using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record VisitDto(
    Guid Id,
    Guid UnitId,
    string UnitName,
    string ClientCompanyName,
    Guid NutritionistId,
    string NutritionistName,
    Guid? ChecklistId,
    string? ChecklistName,
    DateTime ScheduledAt,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    VisitStatus Status,
    string? Notes,
    DateTime CreatedAt,
    int TotalEvaluations,
    int ConformingCount,
    int NonConformingCount,
    double? ComplianceRate
);

public record VisitDetailDto(
    Guid Id,
    Guid UnitId,
    string UnitName,
    string UnitAddress,
    string ClientCompanyName,
    Guid NutritionistId,
    string NutritionistName,
    Guid? ChecklistId,
    string? ChecklistName,
    DateTime ScheduledAt,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    VisitStatus Status,
    string? Notes,
    DateTime CreatedAt,
    List<VisitItemDto> Items,
    List<NonConformityDto> NonConformities,
    double? ComplianceRate
);

public record VisitItemDto(
    Guid Id,
    Guid ChecklistItemId,
    string Category,
    string Description,
    EvaluationResult Result,
    string? Observation,
    Guid? NonConformityId
);

public record CreateVisitRequest(
    Guid UnitId,
    Guid NutritionistId,
    Guid? ChecklistId,
    DateTime ScheduledAt,
    string? Notes
);

public record RecordVisitEvaluationRequest(
    Guid ChecklistItemId,
    EvaluationResult Result,
    string? Observation,
    CreateNonConformityRequest? NonConformity
);

public record FinishVisitRequest(
    string? Notes,
    List<RecordVisitEvaluationRequest>? Evaluations
);
