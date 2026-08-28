using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record NonConformityDto(
    Guid Id,
    Guid VisitId,
    Guid? VisitItemId,
    string UnitName,
    string ClientCompanyName,
    string Category,
    string Description,
    NonConformitySeverity Severity,
    NonConformityStatus Status,
    DateTime? DueDate,
    string? CorrectiveAction,
    bool IsLate,
    DateTime CreatedAt,
    List<ActionItemDto> Actions,
    List<EvidenceDto> Evidences
);

public record ActionItemDto(
    Guid Id,
    Guid NonConformityId,
    string Description,
    Guid? ResponsibleUserId,
    string? ResponsibleUserName,
    DateTime? DueDate,
    ActionItemStatus Status,
    DateTime? CompletedAt,
    string? Notes
);

public record CreateNonConformityRequest(
    string Category,
    string Description,
    NonConformitySeverity Severity,
    DateTime? DueDate,
    string? CorrectiveAction,
    List<string>? InitialEvidenceUrls
);

public record UpdateNonConformityRequest(
    string Category,
    string Description,
    NonConformitySeverity Severity,
    NonConformityStatus Status,
    DateTime? DueDate,
    string? CorrectiveAction
);

public record CreateActionItemRequest(
    string Description,
    Guid? ResponsibleUserId,
    DateTime? DueDate,
    string? Notes
);

public record UpdateActionItemRequest(
    string Description,
    Guid? ResponsibleUserId,
    DateTime? DueDate,
    ActionItemStatus Status,
    string? Notes
);
