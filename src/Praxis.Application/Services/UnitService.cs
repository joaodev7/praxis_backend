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
            .Where(u => !u.IsDeleted);

        if (clientCompanyId.HasValue)
            query = query.Where(u => u.ClientCompanyId == clientCompanyId.Value);

        var units = await query.OrderBy(u => u.Name).ToListAsync();

        return units.Select(u => new UnitDto(
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
            u.Visits.Count
        )).ToList();
    }

    public async Task<UnitDto> GetByIdAsync(Guid id)
    {
        var u = await _context.Units
            .Include(u => u.ClientCompany)
            .Include(u => u.ARTs.Where(a => a.Status == ArtStatus.Active && !a.IsDeleted))
            .Include(u => u.Visits.Where(v => !v.IsDeleted))
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

        if (u == null) throw new KeyNotFoundException("Unidade não encontrada.");

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
            u.Visits.Count
        );
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
        var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
        if (unit == null) throw new KeyNotFoundException("Unidade não encontrada.");

        unit.IsDeleted = true;
        unit.DeletedAt = DateTime.UtcNow;
        unit.Status = CommonStatus.Inactive;

        await _context.SaveChangesAsync();
    }
}
