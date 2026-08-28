using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Application.Services;

public class ChecklistService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ChecklistService(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<ChecklistDto>> GetAllAsync()
    {
        var checklists = await _context.Checklists
            .Include(c => c.Items.Where(i => !i.IsDeleted))
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return checklists.Select(c => new ChecklistDto(
            c.Id,
            c.Name,
            c.Description,
            c.Status,
            c.CreatedAt,
            c.Items.OrderBy(i => i.Order).Select(i => new ChecklistItemDto(
                i.Id,
                i.ChecklistId,
                i.Category,
                i.Description,
                i.Order,
                i.Required,
                i.Status
            )).ToList()
        )).ToList();
    }

    public async Task<ChecklistDto> GetByIdAsync(Guid id)
    {
        var c = await _context.Checklists
            .Include(c => c.Items.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (c == null) throw new KeyNotFoundException("Checklist não encontrado.");

        return new ChecklistDto(
            c.Id,
            c.Name,
            c.Description,
            c.Status,
            c.CreatedAt,
            c.Items.OrderBy(i => i.Order).Select(i => new ChecklistItemDto(
                i.Id,
                i.ChecklistId,
                i.Category,
                i.Description,
                i.Order,
                i.Required,
                i.Status
            )).ToList()
        );
    }

    public async Task<ChecklistDto> CreateAsync(CreateChecklistRequest request)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var checklist = new Checklist
        {
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            Status = CommonStatus.Active,
            Items = request.Items.Select(i => new ChecklistItem
            {
                Category = i.Category,
                Description = i.Description,
                Order = i.Order,
                Required = i.Required,
                Status = CommonStatus.Active
            }).ToList()
        };

        _context.Checklists.Add(checklist);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(checklist.Id);
    }

    public async Task<ChecklistDto> UpdateAsync(Guid id, UpdateChecklistRequest request)
    {
        var checklist = await _context.Checklists
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (checklist == null) throw new KeyNotFoundException("Checklist não encontrado.");

        checklist.Name = request.Name;
        checklist.Description = request.Description;
        checklist.Status = request.Status;
        checklist.UpdatedAt = DateTime.UtcNow;

        if (request.Items != null)
        {
            foreach (var itemReq in request.Items)
            {
                if (itemReq.Id.HasValue)
                {
                    var existing = checklist.Items.FirstOrDefault(i => i.Id == itemReq.Id.Value);
                    if (existing != null)
                    {
                        existing.Category = itemReq.Category;
                        existing.Description = itemReq.Description;
                        existing.Order = itemReq.Order;
                        existing.Required = itemReq.Required;
                        existing.Status = itemReq.Status;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    checklist.Items.Add(new ChecklistItem
                    {
                        ChecklistId = checklist.Id,
                        Category = itemReq.Category,
                        Description = itemReq.Description,
                        Order = itemReq.Order,
                        Required = itemReq.Required,
                        Status = CommonStatus.Active
                    });
                }
            }
        }

        await _context.SaveChangesAsync();

        return await GetByIdAsync(checklist.Id);
    }
}
