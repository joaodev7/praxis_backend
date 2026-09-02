using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Application.Services;

public class UnitService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UnitService(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<UnitDto>> GetAllAsync(Guid? clientCompanyId = null)
    {
        var query = _context.Units
            .Include(u => u.ClientCompany)
            .Include(u => u.ARTs.Where(a => a.Status == ArtStatus.Active && !a.IsDeleted))
            .Include(u => u.Visits.Where(v => !v.IsDeleted))
            .Include(u => u.NutritionistAssignments)
                .ThenInclude(na => na.Nutritionist)
                    .ThenInclude(n => n!.User)
            .Where(u => !u.IsDeleted);

        if (clientCompanyId.HasValue)
            query = query.Where(u => u.ClientCompanyId == clientCompanyId.Value);

        var units = await query.OrderBy(u => u.Name).ToListAsync();

        return units.Select(MapToDto).ToList();
    }

    public async Task<UnitDto> GetByIdAsync(Guid id)
    {
        var u = await _context.Units
            .Include(u => u.ClientCompany)
            .Include(u => u.ARTs.Where(a => a.Status == ArtStatus.Active && !a.IsDeleted))
            .Include(u => u.Visits.Where(v => !v.IsDeleted))
            .Include(u => u.NutritionistAssignments)
                .ThenInclude(na => na.Nutritionist)
                    .ThenInclude(n => n!.User)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

        if (u == null) throw new KeyNotFoundException("Unidade não encontrada.");

        return MapToDto(u);
    }

    public async Task<UnitDto> CreateAsync(CreateUnitRequest request)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var client = await _context.ClientCompanies.FirstOrDefaultAsync(c => c.Id == request.ClientCompanyId && !c.IsDeleted);
        if (client == null) throw new KeyNotFoundException("Empresa cliente vinculada não encontrada.");

        var unit = new Unit
        {
            TenantId = tenantId,
            ClientCompanyId = request.ClientCompanyId,
            Name = request.Name,
            Address = request.Address,
            Phone = request.Phone,
            ResponsibleName = request.ResponsibleName,
            Notes = request.Notes,
            Status = CommonStatus.Active
        };

        _context.Units.Add(unit);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(unit.Id);
    }

    public async Task<UnitDto> UpdateAsync(Guid id, UpdateUnitRequest request)
    {
        var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
        if (unit == null) throw new KeyNotFoundException("Unidade não encontrada.");

        unit.Name = request.Name;
        unit.Address = request.Address;
        unit.Phone = request.Phone;
        unit.ResponsibleName = request.ResponsibleName;
        unit.Notes = request.Notes;
        unit.Status = request.Status;
        unit.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(unit.Id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var unit = await _context.Units
            .Include(u => u.NutritionistAssignments)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

        if (unit == null) throw new KeyNotFoundException("Unidade não encontrada.");

        unit.IsDeleted = true;
        unit.DeletedAt = DateTime.UtcNow;
        unit.Status = CommonStatus.Inactive;

        await _context.SaveChangesAsync();
    }

    public async Task<UnitDto> AllocateNutritionistAsync(Guid unitId, Guid nutritionistId)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var unit = await _context.Units
            .Include(u => u.NutritionistAssignments)
            .FirstOrDefaultAsync(u => u.Id == unitId && !u.IsDeleted);

        if (unit == null) throw new KeyNotFoundException("Unidade não encontrada.");

        var nutritionist = await _context.Nutritionists
            .FirstOrDefaultAsync(n => n.Id == nutritionistId && !n.IsDeleted);

        if (nutritionist == null) throw new KeyNotFoundException("Nutricionista não encontrado.");

        var alreadyAssigned = unit.NutritionistAssignments.Any(na => na.NutritionistId == nutritionistId);
        if (!alreadyAssigned)
        {
            _context.NutritionistUnitAssignments.Add(new NutritionistUnitAssignment
            {
                TenantId = tenantId,
                UnitId = unitId,
                NutritionistId = nutritionistId
            });

            await _context.SaveChangesAsync();
        }

        return await GetByIdAsync(unitId);
    }

    public async Task<UnitDto> DeallocateNutritionistAsync(Guid unitId, Guid nutritionistId)
    {
        var unit = await _context.Units
            .FirstOrDefaultAsync(u => u.Id == unitId && !u.IsDeleted);

        if (unit == null) throw new KeyNotFoundException("Unidade não encontrada.");

        var assignment = await _context.NutritionistUnitAssignments
            .FirstOrDefaultAsync(na => na.UnitId == unitId && na.NutritionistId == nutritionistId);

        if (assignment == null)
            throw new KeyNotFoundException("Vínculo entre o nutricionista e a unidade não encontrado.");

        _context.NutritionistUnitAssignments.Remove(assignment);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(unitId);
    }

    private static UnitDto MapToDto(Unit u)
    {
        var assignedNutritionists = u.NutritionistAssignments
            .Where(na => na.Nutritionist != null && !na.Nutritionist.IsDeleted)
            .Select(na => new AssignedNutritionistDto(
                na.NutritionistId,
                na.Nutritionist!.UserId,
                na.Nutritionist.User?.Name ?? string.Empty,
                na.Nutritionist.User?.Email ?? string.Empty,
                na.Nutritionist.Crn,
                na.Nutritionist.Phone,
                na.Nutritionist.Status
            ))
            .ToList();

        return new UnitDto(
            u.Id,
            u.ClientCompanyId,
            u.ClientCompany?.TradeName ?? string.Empty,
            u.Name,
            u.Address,
            u.Phone,
            u.ResponsibleName,
            u.Notes,
            u.Status,
            u.CreatedAt,
            u.ARTs.FirstOrDefault()?.Number,
            u.Visits.Count,
            assignedNutritionists
        );
    }
}
