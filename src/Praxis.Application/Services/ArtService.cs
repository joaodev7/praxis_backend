using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Application.Services;

public class ArtService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ArtService(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<ArtDto>> GetAllAsync()
    {
        var arts = await _context.ARTs
            .Include(a => a.Unit)
                .ThenInclude(u => u!.ClientCompany)
            .Include(a => a.Nutritionist)
                .ThenInclude(n => n!.User)
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.StartDate)
            .ToListAsync();

        return arts.Select(a => new ArtDto(
            a.Id,
            a.UnitId,
            a.Unit?.Name ?? string.Empty,
            a.Unit?.ClientCompany?.TradeName ?? string.Empty,
            a.NutritionistId,
            a.Nutritionist?.User?.Name ?? string.Empty,
            a.Number,
            a.StartDate,
            a.EndDate,
            a.Status,
            a.DocumentUrl,
            a.Notes,
            a.CreatedAt
        )).ToList();
    }

    public async Task<ArtDto> GetByIdAsync(Guid id)
    {
        var a = await _context.ARTs
            .Include(a => a.Unit)
                .ThenInclude(u => u!.ClientCompany)
            .Include(a => a.Nutritionist)
                .ThenInclude(n => n!.User)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

        if (a == null) throw new KeyNotFoundException("ART não encontrada.");

        return new ArtDto(
            a.Id,
            a.UnitId,
            a.Unit?.Name ?? string.Empty,
            a.Unit?.ClientCompany?.TradeName ?? string.Empty,
            a.NutritionistId,
            a.Nutritionist?.User?.Name ?? string.Empty,
            a.Number,
            a.StartDate,
            a.EndDate,
            a.Status,
            a.DocumentUrl,
            a.Notes,
            a.CreatedAt
        );
    }

    public async Task<ArtDto> CreateAsync(CreateArtRequest request)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var unitExists = await _context.Units.AnyAsync(u => u.Id == request.UnitId && !u.IsDeleted);
        if (!unitExists) throw new KeyNotFoundException("Unidade não encontrada.");

        var nutritionistExists = await _context.Nutritionists.AnyAsync(n => n.Id == request.NutritionistId && !n.IsDeleted);
        if (!nutritionistExists) throw new KeyNotFoundException("Nutricionista não encontrado.");

        var art = new ART
        {
            TenantId = tenantId,
            UnitId = request.UnitId,
            NutritionistId = request.NutritionistId,
            Number = request.Number,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            DocumentUrl = request.DocumentUrl,
            Notes = request.Notes,
            Status = ArtStatus.Active
        };

        _context.ARTs.Add(art);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(art.Id);
    }

    public async Task<ArtDto> UpdateAsync(Guid id, UpdateArtRequest request)
    {
        var art = await _context.ARTs.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
        if (art == null) throw new KeyNotFoundException("ART não encontrada.");

        art.Number = request.Number;
        art.StartDate = request.StartDate;
        art.EndDate = request.EndDate;
        art.Status = request.Status;
        art.DocumentUrl = request.DocumentUrl;
        art.Notes = request.Notes;
        art.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(art.Id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var art = await _context.ARTs.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
        if (art == null) throw new KeyNotFoundException("ART não encontrada.");

        art.IsDeleted = true;
        art.DeletedAt = DateTime.UtcNow;
        art.Status = ArtStatus.Ended;

        await _context.SaveChangesAsync();
    }
}
