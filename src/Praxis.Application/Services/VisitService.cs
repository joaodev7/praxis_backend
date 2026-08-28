using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Application.Services;

public class VisitService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public VisitService(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<VisitDto>> GetAllAsync(Guid? nutritionistId = null, Guid? unitId = null, VisitStatus? status = null)
    {
        var query = _context.Visits
            .Include(v => v.Unit)
                .ThenInclude(u => u!.ClientCompany)
            .Include(v => v.Nutritionist)
                .ThenInclude(n => n!.User)
            .Include(v => v.Checklist)
            .Include(v => v.Items)
            .Where(v => !v.IsDeleted);

        if (nutritionistId.HasValue)
            query = query.Where(v => v.NutritionistId == nutritionistId.Value);

        if (unitId.HasValue)
            query = query.Where(v => v.UnitId == unitId.Value);

        if (status.HasValue)
            query = query.Where(v => v.Status == status.Value);

        var visits = await query.OrderByDescending(v => v.ScheduledAt).ToListAsync();

        return visits.Select(v =>
        {
            var conforming = v.Items.Count(i => i.Result == EvaluationResult.Conforme);
            var nonConforming = v.Items.Count(i => i.Result == EvaluationResult.NaoConforme);
            var evaluated = conforming + nonConforming;
            double? compliance = evaluated > 0 ? Math.Round((double)conforming / evaluated * 100, 1) : null;

            return new VisitDto(
                v.Id,
                v.UnitId,
                v.Unit?.Name ?? string.Empty,
                v.Unit?.ClientCompany?.TradeName ?? string.Empty,
                v.NutritionistId,
                v.Nutritionist?.User?.Name ?? string.Empty,
                v.ChecklistId,
                v.Checklist?.Name,
                v.ScheduledAt,
                v.StartedAt,
                v.FinishedAt,
                v.Status,
                v.Notes,
                v.CreatedAt,
                v.Items.Count,
                conforming,
                nonConforming,
                compliance
            );
        }).ToList();
    }

    public async Task<VisitDetailDto> GetByIdAsync(Guid id)
    {
        var v = await _context.Visits
            .Include(v => v.Unit)
                .ThenInclude(u => u!.ClientCompany)
            .Include(v => v.Nutritionist)
                .ThenInclude(n => n!.User)
            .Include(v => v.Checklist)
            .Include(v => v.Items)
                .ThenInclude(i => i.ChecklistItem)
            .Include(v => v.Items)
                .ThenInclude(i => i.NonConformity)
            .Include(v => v.NonConformities)
                .ThenInclude(nc => nc.Actions)
            .Include(v => v.NonConformities)
                .ThenInclude(nc => nc.Evidences)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);

        if (v == null) throw new KeyNotFoundException("Visita técnica não encontrada.");

        var conforming = v.Items.Count(i => i.Result == EvaluationResult.Conforme);
        var nonConforming = v.Items.Count(i => i.Result == EvaluationResult.NaoConforme);
        var evaluated = conforming + nonConforming;
        double? compliance = evaluated > 0 ? Math.Round((double)conforming / evaluated * 100, 1) : null;

        var itemsDto = v.Items.Select(i => new VisitItemDto(
            i.Id,
            i.ChecklistItemId,
            i.ChecklistItem?.Category ?? string.Empty,
            i.ChecklistItem?.Description ?? string.Empty,
            i.Result,
            i.Observation,
            i.NonConformity?.Id
        )).ToList();

        var ncDtos = v.NonConformities.Where(nc => !nc.IsDeleted).Select(nc => new NonConformityDto(
            nc.Id,
            nc.VisitId,
            nc.VisitItemId,
            v.Unit?.Name ?? string.Empty,
            v.Unit?.ClientCompany?.TradeName ?? string.Empty,
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
                null,
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

        return new VisitDetailDto(
            v.Id,
            v.UnitId,
            v.Unit?.Name ?? string.Empty,
            v.Unit?.Address ?? string.Empty,
            v.Unit?.ClientCompany?.TradeName ?? string.Empty,
            v.NutritionistId,
            v.Nutritionist?.User?.Name ?? string.Empty,
            v.ChecklistId,
            v.Checklist?.Name,
            v.ScheduledAt,
            v.StartedAt,
            v.FinishedAt,
            v.Status,
            v.Notes,
            v.CreatedAt,
            itemsDto,
            ncDtos,
            compliance
        );
    }

    public async Task<VisitDto> CreateAsync(CreateVisitRequest request)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == request.UnitId && !u.IsDeleted);
        if (unit == null) throw new KeyNotFoundException("Unidade não encontrada.");

        var nutritionist = await _context.Nutritionists.FirstOrDefaultAsync(n => n.Id == request.NutritionistId && !n.IsDeleted);
        if (nutritionist == null) throw new KeyNotFoundException("Nutricionista não encontrado.");

        Guid? checklistId = request.ChecklistId;
        if (!checklistId.HasValue)
        {
            var defaultChecklist = await _context.Checklists.FirstOrDefaultAsync(c => !c.IsDeleted && c.Status == CommonStatus.Active);
            checklistId = defaultChecklist?.Id;
        }

        var visit = new Visit
        {
            TenantId = tenantId,
            UnitId = request.UnitId,
            NutritionistId = request.NutritionistId,
            ChecklistId = checklistId,
            ScheduledAt = request.ScheduledAt,
            Status = VisitStatus.Scheduled,
            Notes = request.Notes
        };

        _context.Visits.Add(visit);
        await _context.SaveChangesAsync();

        var created = await GetByIdAsync(visit.Id);
        return new VisitDto(created.Id, created.UnitId, created.UnitName, created.ClientCompanyName, created.NutritionistId, created.NutritionistName, created.ChecklistId, created.ChecklistName, created.ScheduledAt, created.StartedAt, created.FinishedAt, created.Status, created.Notes, created.CreatedAt, 0, 0, 0, null);
    }

    public async Task<VisitDetailDto> StartVisitAsync(Guid id)
    {
        var visit = await _context.Visits
            .Include(v => v.Checklist)
                .ThenInclude(c => c!.Items.Where(i => !i.IsDeleted))
            .Include(v => v.Items)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);

        if (visit == null) throw new KeyNotFoundException("Visita técnica não encontrada.");

        if (visit.Status == VisitStatus.Finished)
            throw new InvalidOperationException("Esta visita já foi finalizada.");

        visit.StartedAt ??= DateTime.UtcNow;
        visit.Status = VisitStatus.InProgress;
        visit.UpdatedAt = DateTime.UtcNow;

        // Populate items from checklist if not yet populated
        if (!visit.Items.Any() && visit.Checklist != null)
        {
            foreach (var checklistItem in visit.Checklist.Items.OrderBy(i => i.Order))
            {
                visit.Items.Add(new VisitItem
                {
                    VisitId = visit.Id,
                    ChecklistItemId = checklistItem.Id,
                    Result = EvaluationResult.Conforme,
                    Observation = null
                });
            }
        }

        await _context.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<VisitDetailDto> FinishVisitAsync(Guid id, FinishVisitRequest request)
    {
        var visit = await _context.Visits
            .Include(v => v.Items)
            .Include(v => v.NonConformities)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);

        if (visit == null) throw new KeyNotFoundException("Visita técnica não encontrada.");

        visit.FinishedAt = DateTime.UtcNow;
        visit.Status = VisitStatus.Finished;
        if (!string.IsNullOrWhiteSpace(request.Notes))
            visit.Notes = request.Notes;
        visit.UpdatedAt = DateTime.UtcNow;

        if (request.Evaluations != null && request.Evaluations.Any())
        {
            foreach (var eval in request.Evaluations)
            {
                var existingItem = visit.Items.FirstOrDefault(i => i.ChecklistItemId == eval.ChecklistItemId);
                if (existingItem == null)
                {
                    existingItem = new VisitItem
                    {
                        VisitId = visit.Id,
                        ChecklistItemId = eval.ChecklistItemId,
                        Result = eval.Result,
                        Observation = eval.Observation
                    };
                    visit.Items.Add(existingItem);
                }
                else
                {
                    existingItem.Result = eval.Result;
                    existingItem.Observation = eval.Observation;
                }

                // If non-conforming, register or link NonConformity
                if (eval.Result == EvaluationResult.NaoConforme && eval.NonConformity != null)
                {
                    var checklistItem = await _context.ChecklistItems.FirstOrDefaultAsync(ci => ci.Id == eval.ChecklistItemId);
                    var category = !string.IsNullOrWhiteSpace(eval.NonConformity.Category) ? eval.NonConformity.Category : (checklistItem?.Category ?? "Geral");
                    var desc = !string.IsNullOrWhiteSpace(eval.NonConformity.Description) ? eval.NonConformity.Description : (checklistItem?.Description ?? "Não conformidade identificada");

                    var nc = new NonConformity
                    {
                        TenantId = visit.TenantId,
                        VisitId = visit.Id,
                        VisitItemId = existingItem.Id,
                        Category = category,
                        Description = desc,
                        Severity = eval.NonConformity.Severity,
                        Status = NonConformityStatus.Aberta,
                        DueDate = eval.NonConformity.DueDate ?? DateTime.UtcNow.AddDays(7),
                        CorrectiveAction = eval.NonConformity.CorrectiveAction
                    };

                    if (eval.NonConformity.InitialEvidenceUrls != null)
                    {
                        foreach (var url in eval.NonConformity.InitialEvidenceUrls)
                        {
                            nc.Evidences.Add(new Evidence
                            {
                                TenantId = visit.TenantId,
                                NonConformityId = nc.Id,
                                Type = EvidenceType.Photo,
                                Url = url,
                                Description = "Evidência fotográfica registrada durante a visita",
                                UploadedByUserId = _currentUser.UserId
                            });
                        }
                    }

                    _context.NonConformities.Add(nc);
                }
            }
        }

        await _context.SaveChangesAsync();
        return await GetByIdAsync(id);
    }
}
