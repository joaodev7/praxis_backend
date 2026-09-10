using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record ActionPlanDto(
    Guid Id,
    Guid NonConformityId,
    string NonConformityDescription,
    Guid? VisitId,
    Guid? UnitId,
    string UnitName,
    Guid? ClientCompanyId,
    string ClientCompanyName,
    string What,
    string Description,
    string? Why,
    Guid? ResponsibleUserId,
    string? ResponsibleUserName,
    string? ResponsibleName,
    DateTime? DueDate,
    string? Where,
    string? How,
    decimal? HowMuch,
    ActionPlanPriority Priority,
    ActionItemStatus Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime? ValidatedAt,
    Guid? ValidatedByUserId,
    string? ValidatedByUserName,
    string? ValidationComment,
    string? CancellationReason,
    string? Notes,
    bool IsLate,
    DateTime CreatedAt,
    List<ActionPlanEvidenceDto> Evidences
);

public record ActionPlanEvidenceDto(
    Guid Id,
    Guid ActionPlanId,
    string FileUrl,
    string ObjectKey,
    string FileName,
    string ContentType,
    long FileSize,
    DateTime UploadedAt,
    Guid? UploadedByUserId,
    string? UploadedByUserName
);

public record CreateActionPlanRequest(
    Guid NonConformityId,
    string What,
    string? Why,
    Guid? ResponsibleUserId,
    string? ResponsibleName,
    DateTime DueDate,
    string? Where,
    string? How,
    decimal? HowMuch,
    ActionPlanPriority Priority = ActionPlanPriority.Media,
    string? Notes = null
);

public record UpdateActionPlanRequest(
    string What,
    string? Why,
    Guid? ResponsibleUserId,
    string? ResponsibleName,
    DateTime DueDate,
    string? Where,
    string? How,
    decimal? HowMuch,
    ActionPlanPriority Priority,
    string? Notes
);

public record ChangeActionPlanStatusRequest(
    ActionItemStatus Status,
    string? Notes
);

public record ValidateActionPlanRequest(
    bool Approved,
    string? Comment
);

public record CancelActionPlanRequest(
    string Reason
);

public record ActionPlanDashboardDto(
    int TotalActions,
    int PendingActions,
    int InProgressActions,
    int WaitingValidationActions,
    int CompletedActions,
    int CancelledActions,
    int LateActions,
    List<ClientActionPlanMetricDto> ClientMetrics
);

public record ClientActionPlanMetricDto(
    Guid ClientCompanyId,
    string ClientCompanyName,
    int TotalActions,
    int CompletedActions,
    int InProgressActions,
    int PendingActions,
    int WaitingValidationActions,
    int LateActions
);
