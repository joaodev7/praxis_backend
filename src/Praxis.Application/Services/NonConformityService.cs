using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Application.Services;

public class NonConformityService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public NonConformityService(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<NonConformityDto>> GetAllAsync(NonConformityStatus? status = null, NonConformitySeverity? severity = null, Guid? unitId = null, Guid? visitId = null)
    {
        var query = _context.NonConformities
            .Include(nc => nc.Visit)
                .ThenInclude(v => v!.Unit)
                    .ThenInclude(u => u!.ClientCompany)
            .Include(nc => nc.Actions)
                .ThenInclude(a => a.ResponsibleUser)
            .Include(nc => nc.Evidences)
            .AsSplitQuery()
            .Where(nc => !nc.IsDeleted);

        if (status.HasValue)
            query = query.Where(nc => nc.Status == status.Value);

        if (severity.HasValue)
            query = query.Where(nc => nc.Severity == severity.Value);

        if (unitId.HasValue)
            query = query.Where(nc => nc.Visit!.UnitId == unitId.Value);

        if (visitId.HasValue)
            query = query.Where(nc => nc.VisitId == visitId.Value);

        var list = await query.OrderByDescending(nc => nc.CreatedAt).ToListAsync();

        return list.Select(nc => new NonConformityDto(
            nc.Id,
            nc.VisitId,
            nc.VisitItemId,
            nc.Visit?.Unit?.Name ?? string.Empty,
            nc.Visit?.Unit?.ClientCompany?.TradeName ?? string.Empty,
            nc.Category,
            nc.Description,
            nc.Severity,
            nc.Status,
            nc.DueDate,
            nc.CorrectiveAction,
            nc.Status != NonConformityStatus.Resolvida && nc.DueDate.HasValue && nc.DueDate.Value < DateTime.UtcNow,
            nc.CreatedAt,
            nc.Actions.Where(a => !a.IsDeleted).Select(a => new ActionItemDto(
                a.Id,
                a.NonConformityId,
                a.Description,
                a.ResponsibleUserId,
                a.ResponsibleUser?.Name ?? a.ResponsibleName,
                a.DueDate,
                a.Status,
                a.CompletedAt,
                a.Notes
            )).ToList(),
            nc.Evidences.Select(e => new EvidenceDto(
                e.Id,
                e.NonConformityId,
                e.Type,
                e.Url,
                e.Description,
                e.CreatedAt,
                e.UploadedByUserId
            )).ToList()
        )).ToList();
    }

    public async Task<NonConformityDto> GetByIdAsync(Guid id)
    {
        var nc = await _context.NonConformities
            .Include(nc => nc.Visit)
                .ThenInclude(v => v!.Unit)
                    .ThenInclude(u => u!.ClientCompany)
            .Include(nc => nc.Actions)
                .ThenInclude(a => a.ResponsibleUser)
            .Include(nc => nc.Evidences)
            .AsSplitQuery()
            .FirstOrDefaultAsync(nc => nc.Id == id && !nc.IsDeleted);

        if (nc == null) throw new KeyNotFoundException("Não conformidade não encontrada.");

        return new NonConformityDto(
            nc.Id,
            nc.VisitId,
            nc.VisitItemId,
            nc.Visit?.Unit?.Name ?? string.Empty,
            nc.Visit?.Unit?.ClientCompany?.TradeName ?? string.Empty,
            nc.Category,
            nc.Description,
            nc.Severity,
            nc.Status,
            nc.DueDate,
            nc.CorrectiveAction,
            nc.Status != NonConformityStatus.Resolvida && nc.DueDate.HasValue && nc.DueDate.Value < DateTime.UtcNow,
            nc.CreatedAt,
            nc.Actions.Where(a => !a.IsDeleted).Select(a => new ActionItemDto(
                a.Id,
                a.NonConformityId,
                a.Description,
                a.ResponsibleUserId,
                a.ResponsibleUser?.Name ?? a.ResponsibleName,
                a.DueDate,
                a.Status,
                a.CompletedAt,
                a.Notes
            )).ToList(),
            nc.Evidences.Select(e => new EvidenceDto(
                e.Id,
                e.NonConformityId,
                e.Type,
                e.Url,
                e.Description,
                e.CreatedAt,
                e.UploadedByUserId
            )).ToList()
        );
    }

    public async Task<NonConformityDto> CreateForVisitAsync(Guid visitId, CreateNonConformityRequest request)
    {
        var visit = await _context.Visits
            .Include(v => v.Unit)
            .FirstOrDefaultAsync(v => v.Id == visitId && !v.IsDeleted);

        if (visit == null) throw new KeyNotFoundException("Visita técnica/Auditoria não encontrada.");

        var nc = new NonConformity
        {
            TenantId = visit.TenantId,
            VisitId = visitId,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Geral" : request.Category.Trim(),
            Description = request.Description.Trim(),
            Severity = request.Severity,
            Status = NonConformityStatus.Aberta,
            DueDate = request.DueDate ?? DateTime.UtcNow.AddDays(7),
            CorrectiveAction = request.CorrectiveAction?.Trim()
        };

        if (request.InitialEvidenceUrls != null)
        {
            foreach (var url in request.InitialEvidenceUrls)
            {
                nc.Evidences.Add(new Evidence
                {
                    TenantId = visit.TenantId,
                    Type = EvidenceType.Photo,
                    Url = url,
                    Description = "Evidência fotográfica registrada",
                    UploadedByUserId = _currentUser.UserId
                });
            }
        }

        _context.NonConformities.Add(nc);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(nc.Id);
    }

    public async Task<NonConformityDto> UpdateAsync(Guid id, UpdateNonConformityRequest request)
    {
        var nc = await _context.NonConformities.FirstOrDefaultAsync(nc => nc.Id == id && !nc.IsDeleted);
        if (nc == null) throw new KeyNotFoundException("Não conformidade não encontrada.");

        nc.Category = request.Category;
        nc.Description = request.Description;
        nc.Severity = request.Severity;
        nc.Status = request.Status;
        nc.DueDate = request.DueDate;
        nc.CorrectiveAction = request.CorrectiveAction;
        nc.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await GetByIdAsync(nc.Id);
    }

    public async Task<ActionItemDto> AddActionItemAsync(Guid nonConformityId, CreateActionItemRequest request)
    {
        var nc = await _context.NonConformities.FirstOrDefaultAsync(n => n.Id == nonConformityId && !n.IsDeleted);
        if (nc == null) throw new KeyNotFoundException("Não conformidade não encontrada.");

        var action = new ActionItem
        {
            NonConformityId = nonConformityId,
            Description = request.Description,
            ResponsibleUserId = request.ResponsibleUserId,
            DueDate = request.DueDate,
            Notes = request.Notes,
            Status = ActionItemStatus.Pendente
        };

        _context.ActionItems.Add(action);
        await _context.SaveChangesAsync();

        return new ActionItemDto(action.Id, action.NonConformityId, action.Description, action.ResponsibleUserId, null, action.DueDate, action.Status, action.CompletedAt, action.Notes);
    }

    public async Task<ActionItemDto> UpdateActionItemAsync(Guid nonConformityId, Guid actionId, UpdateActionItemRequest request)
    {
        var action = await _context.ActionItems.FirstOrDefaultAsync(a => a.Id == actionId && a.NonConformityId == nonConformityId && !a.IsDeleted);
        if (action == null) throw new KeyNotFoundException("Item de ação não encontrado.");

        action.Description = request.Description;
        action.ResponsibleUserId = request.ResponsibleUserId;
        action.DueDate = request.DueDate;
        action.Status = request.Status;
        action.Notes = request.Notes;
        if (request.Status == ActionItemStatus.Concluida && action.CompletedAt == null)
            action.CompletedAt = DateTime.UtcNow;
        action.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return new ActionItemDto(action.Id, action.NonConformityId, action.Description, action.ResponsibleUserId, null, action.DueDate, action.Status, action.CompletedAt, action.Notes);
    }
}
