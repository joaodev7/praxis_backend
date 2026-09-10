using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Application.Services;

public class ActionPlanService : IActionPlanService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<ActionPlanService> _logger;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".pdf"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB

    public ActionPlanService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IFileStorageService fileStorage,
        ILogger<ActionPlanService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    private Guid RequireTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue || tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("Tenant não identificado ou usuário não autenticado.");
        return tenantId.Value;
    }

    private bool IsAuthorizedToValidateOrCancel()
    {
        var role = _currentUser.Role;
        return role == UserRole.PraxisAdmin || role == UserRole.TenantAdmin || role == UserRole.Nutritionist;
    }

    public async Task<List<ActionPlanDto>> GetAllAsync(
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
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        var query = _context.ActionItems
            .Include(a => a.ResponsibleUser)
            .Include(a => a.ValidatedByUser)
            .Include(a => a.Evidences.Where(e => !e.IsDeleted))
                .ThenInclude(e => e.UploadedByUser)
            .Include(a => a.NonConformity)
                .ThenInclude(nc => nc!.Visit)
                    .ThenInclude(v => v!.Unit)
                        .ThenInclude(u => u!.ClientCompany)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted);

        if (nonConformityId.HasValue)
            query = query.Where(a => a.NonConformityId == nonConformityId.Value);

        if (visitId.HasValue)
            query = query.Where(a => a.NonConformity != null && a.NonConformity.VisitId == visitId.Value);

        if (unitId.HasValue)
            query = query.Where(a => a.NonConformity != null && a.NonConformity.Visit != null && a.NonConformity.Visit.UnitId == unitId.Value);

        if (clientId.HasValue)
            query = query.Where(a => a.NonConformity != null && a.NonConformity.Visit != null && a.NonConformity.Visit.Unit != null && a.NonConformity.Visit.Unit.ClientCompanyId == clientId.Value);

        if (responsibleUserId.HasValue)
            query = query.Where(a => a.ResponsibleUserId == responsibleUserId.Value);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (priority.HasValue)
            query = query.Where(a => a.Priority == priority.Value);

        if (startDate.HasValue)
            query = query.Where(a => a.DueDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.DueDate <= endDate.Value);

        var now = DateTime.UtcNow;
        if (isLate.HasValue)
        {
            if (isLate.Value)
            {
                query = query.Where(a => a.Status != ActionItemStatus.Concluida &&
                                         a.Status != ActionItemStatus.Cancelada &&
                                         a.DueDate.HasValue &&
                                         a.DueDate.Value < now);
            }
            else
            {
                query = query.Where(a => a.Status == ActionItemStatus.Concluida ||
                                         a.Status == ActionItemStatus.Cancelada ||
                                         !a.DueDate.HasValue ||
                                         a.DueDate.Value >= now);
            }
        }

        var list = await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = new List<ActionPlanDto>(list.Count);
        foreach (var item in list)
        {
            result.Add(await MapToDtoAsync(item, cancellationToken));
        }

        return result;
    }

    public async Task<ActionPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        var item = await _context.ActionItems
            .Include(a => a.ResponsibleUser)
            .Include(a => a.ValidatedByUser)
            .Include(a => a.Evidences.Where(e => !e.IsDeleted))
                .ThenInclude(e => e.UploadedByUser)
            .Include(a => a.NonConformity)
                .ThenInclude(nc => nc!.Visit)
                    .ThenInclude(v => v!.Unit)
                        .ThenInclude(u => u!.ClientCompany)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);

        if (item == null)
            throw new KeyNotFoundException($"Plano de ação com ID '{id}' não encontrado.");

        return await MapToDtoAsync(item, cancellationToken);
    }

    public async Task<ActionPlanDto> CreateAsync(CreateActionPlanRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        if (string.IsNullOrWhiteSpace(request.What))
            throw new ArgumentException("O campo 'O que será feito' (What) é obrigatório.");

        if (request.DueDate == default)
            throw new ArgumentException("O prazo de conclusão (When/DueDate) é obrigatório.");

        var nonConformity = await _context.NonConformities
            .FirstOrDefaultAsync(nc => nc.Id == request.NonConformityId && nc.TenantId == tenantId && !nc.IsDeleted, cancellationToken);

        if (nonConformity == null)
            throw new KeyNotFoundException($"Não conformidade com ID '{request.NonConformityId}' não encontrada.");

        string? responsibleName = request.ResponsibleName;
        if (request.ResponsibleUserId.HasValue)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.ResponsibleUserId.Value && u.TenantId == tenantId && !u.IsDeleted, cancellationToken);
            if (user == null)
                throw new ArgumentException("Usuário responsável informado não pertence ao tenant ou não foi encontrado.");
            responsibleName ??= user.Name;
        }

        var actionItem = new ActionItem
        {
            TenantId = tenantId,
            NonConformityId = request.NonConformityId,
            What = request.What.Trim(),
            Why = request.Why?.Trim(),
            ResponsibleUserId = request.ResponsibleUserId,
            ResponsibleName = responsibleName?.Trim(),
            DueDate = request.DueDate,
            Where = request.Where?.Trim(),
            How = request.How?.Trim(),
            HowMuch = request.HowMuch,
            Priority = request.Priority,
            Status = ActionItemStatus.Pendente,
            Notes = request.Notes?.Trim()
        };

        _context.ActionItems.Add(actionItem);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Plano de ação {ActionPlanId} criado para Não Conformidade {NonConformityId} por usuário {UserId}.",
            actionItem.Id, nonConformity.Id, _currentUser.UserId);

        return await GetByIdAsync(actionItem.Id, cancellationToken);
    }

    public async Task<ActionPlanDto> UpdateAsync(Guid id, UpdateActionPlanRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        if (string.IsNullOrWhiteSpace(request.What))
            throw new ArgumentException("O campo 'O que será feito' (What) é obrigatório.");

        if (request.DueDate == default)
            throw new ArgumentException("O prazo de conclusão (When/DueDate) é obrigatório.");

        var item = await _context.ActionItems
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);

        if (item == null)
            throw new KeyNotFoundException($"Plano de ação com ID '{id}' não encontrado.");

        if (item.Status == ActionItemStatus.Concluida || item.Status == ActionItemStatus.Cancelada)
            throw new InvalidOperationException("Não é permitido editar um plano de ação concluído ou cancelado.");

        string? responsibleName = request.ResponsibleName;
        if (request.ResponsibleUserId.HasValue)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.ResponsibleUserId.Value && u.TenantId == tenantId && !u.IsDeleted, cancellationToken);
            if (user == null)
                throw new ArgumentException("Usuário responsável informado não pertence ao tenant ou não foi encontrado.");
            responsibleName ??= user.Name;
        }

        item.What = request.What.Trim();
        item.Why = request.Why?.Trim();
        item.ResponsibleUserId = request.ResponsibleUserId;
        item.ResponsibleName = responsibleName?.Trim();
        item.DueDate = request.DueDate;
        item.Where = request.Where?.Trim();
        item.How = request.How?.Trim();
        item.HowMuch = request.HowMuch;
        item.Priority = request.Priority;
        item.Notes = request.Notes?.Trim();
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(item.Id, cancellationToken);
    }

    public async Task<ActionPlanDto> ChangeStatusAsync(Guid id, ChangeActionPlanStatusRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        var item = await _context.ActionItems
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);

        if (item == null)
            throw new KeyNotFoundException($"Plano de ação com ID '{id}' não encontrado.");

        if (item.Status == ActionItemStatus.Cancelada)
            throw new InvalidOperationException("Um plano de ação cancelado não pode ter seu status alterado.");

        // RN03: Não é permitido colocar uma ação em execução sem responsável
        if (request.Status == ActionItemStatus.EmAndamento)
        {
            if (!item.ResponsibleUserId.HasValue && string.IsNullOrWhiteSpace(item.ResponsibleName))
                throw new InvalidOperationException("Não é permitido iniciar a execução de uma ação sem responsável definido.");

            item.StartedAt ??= DateTime.UtcNow;
            item.Status = ActionItemStatus.EmAndamento;
        }
        else if (request.Status == ActionItemStatus.AguardandoValidacao)
        {
            item.CompletedAt ??= DateTime.UtcNow;
            item.Status = ActionItemStatus.AguardandoValidacao;
        }
        else if (request.Status == ActionItemStatus.Pendente)
        {
            item.Status = ActionItemStatus.Pendente;
        }
        else if (request.Status == ActionItemStatus.Concluida)
        {
            // RN06: Apenas usuário autorizado pode validar diretamente para Concluida
            if (!IsAuthorizedToValidateOrCancel())
                throw new UnauthorizedAccessException("Apenas nutricionistas e administradores podem concluir ou validar planos de ação.");

            item.Status = ActionItemStatus.Concluida;
            item.CompletedAt ??= DateTime.UtcNow;
            item.ValidatedAt = DateTime.UtcNow;
            item.ValidatedByUserId = _currentUser.UserId;
        }
        else if (request.Status == ActionItemStatus.Cancelada)
        {
            throw new InvalidOperationException("Para cancelar o plano de ação, utilize a operação de cancelamento informando a justificativa.");
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
            item.Notes = request.Notes.Trim();

        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(item.Id, cancellationToken);
    }

    public async Task<ActionPlanDto> ValidateAsync(Guid id, ValidateActionPlanRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        // RN06: Apenas usuário autorizado pode validar
        if (!IsAuthorizedToValidateOrCancel())
            throw new UnauthorizedAccessException("Apenas nutricionistas e administradores têm permissão para validar ou reprovar planos de ação.");

        var item = await _context.ActionItems
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);

        if (item == null)
            throw new KeyNotFoundException($"Plano de ação com ID '{id}' não encontrado.");

        if (item.Status == ActionItemStatus.Cancelada)
            throw new InvalidOperationException("Não é possível validar um plano de ação cancelado.");

        if (request.Approved)
        {
            item.Status = ActionItemStatus.Concluida;
            item.ValidatedAt = DateTime.UtcNow;
            item.ValidatedByUserId = _currentUser.UserId;
            item.ValidationComment = request.Comment?.Trim();
            if (item.CompletedAt == null)
                item.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Plano de ação {ActionPlanId} validado e concluído pelo usuário {UserId}.", item.Id, _currentUser.UserId);
        }
        else
        {
            // Reprovação / Reabertura: Comentário obrigatório
            if (string.IsNullOrWhiteSpace(request.Comment))
                throw new ArgumentException("É obrigatório informar a justificativa/comentário ao reabrir um plano de ação.");

            item.Status = ActionItemStatus.EmAndamento;
            item.ValidationComment = request.Comment.Trim();
            item.CompletedAt = null;

            _logger.LogInformation("Plano de ação {ActionPlanId} reprovado e reaberto pelo usuário {UserId}. Motivo: {Comment}",
                item.Id, _currentUser.UserId, request.Comment);
        }

        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(item.Id, cancellationToken);
    }

    public async Task<ActionPlanDto> CancelAsync(Guid id, CancelActionPlanRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        // RN06: Apenas usuário autorizado pode cancelar
        if (!IsAuthorizedToValidateOrCancel())
            throw new UnauthorizedAccessException("Apenas nutricionistas e administradores têm permissão para cancelar planos de ação.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("É obrigatório informar o motivo do cancelamento.");

        var item = await _context.ActionItems
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);

        if (item == null)
            throw new KeyNotFoundException($"Plano de ação com ID '{id}' não encontrado.");

        if (item.Status == ActionItemStatus.Concluida)
            throw new InvalidOperationException("Não é permitido cancelar um plano de ação que já foi concluído.");

        item.Status = ActionItemStatus.Cancelada;
        item.CancellationReason = request.Reason.Trim();
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Plano de ação {ActionPlanId} cancelado pelo usuário {UserId}. Motivo: {Reason}",
            item.Id, _currentUser.UserId, request.Reason);

        return await GetByIdAsync(item.Id, cancellationToken);
    }

    public async Task<ActionPlanEvidenceDto> AddEvidenceAsync(
        Guid actionPlanId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        if (fileStream == null || fileStream.Length == 0)
            throw new ArgumentException("Arquivo vazio ou inválido.");

        if (fileSize > MaxFileSizeBytes)
            throw new ArgumentException($"O tamanho do arquivo excede o limite máximo permitido de {MaxFileSizeBytes / (1024 * 1024)}MB.");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new ArgumentException($"Extensão de arquivo '{extension}' não permitida. Extensões aceitas: {string.Join(", ", AllowedExtensions)}.");

        if (!AllowedMimeTypes.Contains(contentType))
            throw new ArgumentException($"Tipo de arquivo '{contentType}' não permitido.");

        var actionPlan = await _context.ActionItems
            .FirstOrDefaultAsync(a => a.Id == actionPlanId && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);

        if (actionPlan == null)
            throw new KeyNotFoundException($"Plano de ação com ID '{actionPlanId}' não encontrado.");

        // Object Key Structure: tenants/{tenantId}/action-plans/{actionPlanId}/evidences/{uuid}.{extension}
        var uniqueFileId = Guid.NewGuid().ToString("N");
        var objectKey = $"tenants/{tenantId}/action-plans/{actionPlanId}/evidences/{uniqueFileId}{extension}";

        fileStream.Position = 0;
        await _fileStorage.UploadAsync(fileStream, objectKey, contentType, cancellationToken);

        var downloadUrl = await _fileStorage.GetFileUrlAsync(objectKey, cancellationToken);

        var evidence = new ActionPlanEvidence
        {
            TenantId = tenantId,
            ActionPlanId = actionPlanId,
            FileName = Path.GetFileName(fileName),
            ContentType = contentType,
            FileSize = fileSize,
            ObjectKey = objectKey,
            FileUrl = downloadUrl ?? string.Empty,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = _currentUser.UserId
        };

        _context.ActionPlanEvidences.Add(evidence);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Evidência {EvidenceId} anexada ao Plano de Ação {ActionPlanId} por usuário {UserId}.",
            evidence.Id, actionPlanId, _currentUser.UserId);

        var uploadedByName = _currentUser.UserId.HasValue
            ? (await _context.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, cancellationToken))?.Name
            : null;

        return new ActionPlanEvidenceDto(
            evidence.Id,
            evidence.ActionPlanId,
            evidence.FileUrl,
            evidence.ObjectKey,
            evidence.FileName,
            evidence.ContentType,
            evidence.FileSize,
            evidence.UploadedAt,
            evidence.UploadedByUserId,
            uploadedByName
        );
    }

    public async Task<List<ActionPlanEvidenceDto>> GetEvidencesAsync(Guid actionPlanId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        var exists = await _context.ActionItems
            .AnyAsync(a => a.Id == actionPlanId && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);

        if (!exists)
            throw new KeyNotFoundException($"Plano de ação com ID '{actionPlanId}' não encontrado.");

        var evidences = await _context.ActionPlanEvidences
            .Include(e => e.UploadedByUser)
            .Where(e => e.ActionPlanId == actionPlanId && e.TenantId == tenantId && !e.IsDeleted)
            .OrderByDescending(e => e.UploadedAt)
            .ToListAsync(cancellationToken);

        var dtos = new List<ActionPlanEvidenceDto>(evidences.Count);
        foreach (var e in evidences)
        {
            var url = await _fileStorage.GetFileUrlAsync(e.ObjectKey, cancellationToken);
            dtos.Add(new ActionPlanEvidenceDto(
                e.Id,
                e.ActionPlanId,
                !string.IsNullOrWhiteSpace(url) ? url : e.FileUrl,
                e.ObjectKey,
                e.FileName,
                e.ContentType,
                e.FileSize,
                e.UploadedAt,
                e.UploadedByUserId,
                e.UploadedByUser?.Name
            ));
        }

        return dtos;
    }

    public async Task<bool> DeleteEvidenceAsync(Guid evidenceId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        var evidence = await _context.ActionPlanEvidences
            .FirstOrDefaultAsync(e => e.Id == evidenceId && e.TenantId == tenantId && !e.IsDeleted, cancellationToken);

        if (evidence == null)
            throw new KeyNotFoundException($"Evidência com ID '{evidenceId}' não encontrada.");

        evidence.IsDeleted = true;
        evidence.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(evidence.ObjectKey))
        {
            try
            {
                await _fileStorage.DeleteAsync(evidence.ObjectKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao remover arquivo do Cloudflare R2 com chave {ObjectKey}.", evidence.ObjectKey);
            }
        }

        return true;
    }

    public async Task<ActionPlanDashboardDto> GetDashboardAsync(Guid? clientId = null, Guid? unitId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        var query = _context.ActionItems
            .Include(a => a.NonConformity)
                .ThenInclude(nc => nc!.Visit)
                    .ThenInclude(v => v!.Unit)
                        .ThenInclude(u => u!.ClientCompany)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted);

        if (clientId.HasValue)
            query = query.Where(a => a.NonConformity != null && a.NonConformity.Visit != null && a.NonConformity.Visit.Unit != null && a.NonConformity.Visit.Unit.ClientCompanyId == clientId.Value);

        if (unitId.HasValue)
            query = query.Where(a => a.NonConformity != null && a.NonConformity.Visit != null && a.NonConformity.Visit.UnitId == unitId.Value);

        var list = await query.ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var total = list.Count;
        var pending = list.Count(a => a.Status == ActionItemStatus.Pendente);
        var inProgress = list.Count(a => a.Status == ActionItemStatus.EmAndamento);
        var waitingValidation = list.Count(a => a.Status == ActionItemStatus.AguardandoValidacao);
        var completed = list.Count(a => a.Status == ActionItemStatus.Concluida);
        var cancelled = list.Count(a => a.Status == ActionItemStatus.Cancelada);
        var late = list.Count(a => a.Status != ActionItemStatus.Concluida &&
                                   a.Status != ActionItemStatus.Cancelada &&
                                   a.DueDate.HasValue &&
                                   a.DueDate.Value < now);

        // Group by Client
        var clientGroups = list
            .Where(a => a.NonConformity?.Visit?.Unit?.ClientCompany != null)
            .GroupBy(a => new
            {
                Id = a.NonConformity!.Visit!.Unit!.ClientCompanyId,
                Name = a.NonConformity.Visit.Unit.ClientCompany!.TradeName
            })
            .Select(g => new ClientActionPlanMetricDto(
                g.Key.Id,
                g.Key.Name,
                g.Count(),
                g.Count(a => a.Status == ActionItemStatus.Concluida),
                g.Count(a => a.Status == ActionItemStatus.EmAndamento),
                g.Count(a => a.Status == ActionItemStatus.Pendente),
                g.Count(a => a.Status == ActionItemStatus.AguardandoValidacao),
                g.Count(a => a.Status != ActionItemStatus.Concluida &&
                             a.Status != ActionItemStatus.Cancelada &&
                             a.DueDate.HasValue &&
                             a.DueDate.Value < now)
            ))
            .OrderByDescending(c => c.TotalActions)
            .ToList();

        return new ActionPlanDashboardDto(
            total,
            pending,
            inProgress,
            waitingValidation,
            completed,
            cancelled,
            late,
            clientGroups
        );
    }

    private async Task<ActionPlanDto> MapToDtoAsync(ActionItem item, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var isLate = item.Status != ActionItemStatus.Concluida &&
                     item.Status != ActionItemStatus.Cancelada &&
                     item.DueDate.HasValue &&
                     item.DueDate.Value < now;

        var evidenceDtos = new List<ActionPlanEvidenceDto>();
        if (item.Evidences != null)
        {
            foreach (var e in item.Evidences.Where(ev => !ev.IsDeleted))
            {
                var url = await _fileStorage.GetFileUrlAsync(e.ObjectKey, cancellationToken);
                evidenceDtos.Add(new ActionPlanEvidenceDto(
                    e.Id,
                    e.ActionPlanId,
                    !string.IsNullOrWhiteSpace(url) ? url : e.FileUrl,
                    e.ObjectKey,
                    e.FileName,
                    e.ContentType,
                    e.FileSize,
                    e.UploadedAt,
                    e.UploadedByUserId,
                    e.UploadedByUser?.Name
                ));
            }
        }

        return new ActionPlanDto(
            item.Id,
            item.NonConformityId,
            item.NonConformity?.Description ?? string.Empty,
            item.NonConformity?.VisitId,
            item.NonConformity?.Visit?.UnitId,
            item.NonConformity?.Visit?.Unit?.Name ?? string.Empty,
            item.NonConformity?.Visit?.Unit?.ClientCompanyId,
            item.NonConformity?.Visit?.Unit?.ClientCompany?.TradeName ?? string.Empty,
            item.What,
            item.Description,
            item.Why,
            item.ResponsibleUserId,
            item.ResponsibleUser?.Name,
            item.ResponsibleName ?? item.ResponsibleUser?.Name,
            item.DueDate,
            item.Where,
            item.How,
            item.HowMuch,
            item.Priority,
            item.Status,
            item.StartedAt,
            item.CompletedAt,
            item.ValidatedAt,
            item.ValidatedByUserId,
            item.ValidatedByUser?.Name,
            item.ValidationComment,
            item.CancellationReason,
            item.Notes,
            isLate,
            item.CreatedAt,
            evidenceDtos
        );
    }
}
