using Praxis.Application.DTOs;
using Praxis.Domain.Enums;

namespace Praxis.Application.Interfaces;

public interface IActionPlanService
{
    Task<List<ActionPlanDto>> GetAllAsync(
        Guid? nonConformityId = null,
        Guid? visitId = null,
        Guid? unitId = null,
        Guid? clientId = null,
        Guid? responsibleUserId = null,
        ActionItemStatus? status = null,
        ActionPlanPriority? priority = null,
        bool? isLate = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    Task<ActionPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ActionPlanDto> CreateAsync(CreateActionPlanRequest request, CancellationToken cancellationToken = default);

    Task<ActionPlanDto> UpdateAsync(Guid id, UpdateActionPlanRequest request, CancellationToken cancellationToken = default);

    Task<ActionPlanDto> ChangeStatusAsync(Guid id, ChangeActionPlanStatusRequest request, CancellationToken cancellationToken = default);

    Task<ActionPlanDto> ValidateAsync(Guid id, ValidateActionPlanRequest request, CancellationToken cancellationToken = default);

    Task<ActionPlanDto> CancelAsync(Guid id, CancelActionPlanRequest request, CancellationToken cancellationToken = default);

    Task<ActionPlanEvidenceDto> AddEvidenceAsync(
        Guid actionPlanId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSize,
        CancellationToken cancellationToken = default);

    Task<List<ActionPlanEvidenceDto>> GetEvidencesAsync(Guid actionPlanId, CancellationToken cancellationToken = default);

    Task<bool> DeleteEvidenceAsync(Guid evidenceId, CancellationToken cancellationToken = default);

    Task<ActionPlanDashboardDto> GetDashboardAsync(Guid? clientId = null, Guid? unitId = null, CancellationToken cancellationToken = default);
}
