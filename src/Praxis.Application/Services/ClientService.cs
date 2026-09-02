using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Application.Services;

public class ClientService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IEntitlementService _entitlementService;

    public ClientService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IEntitlementService entitlementService)
    {
        _context = context;
        _currentUser = currentUser;
        _entitlementService = entitlementService;
    }

    public async Task<List<ClientCompanyDto>> GetAllAsync()
    {
        var clients = await _context.ClientCompanies
            .Include(c => c.Units.Where(u => !u.IsDeleted))
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.TradeName)
            .ToListAsync();

        return clients.Select(c => new ClientCompanyDto(
            c.Id,
            c.LegalName,
            c.TradeName,
            c.Cnpj,
            c.Email,
            c.Phone,
            c.Address,
            c.ResponsibleName,
            c.Notes,
            c.Status,
            c.CreatedAt,
            c.Units.Count
        )).ToList();
    }

    public async Task<ClientCompanyDto> GetByIdAsync(Guid id)
    {
        var c = await _context.ClientCompanies
            .Include(c => c.Units.Where(u => !u.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (c == null) throw new KeyNotFoundException("Empresa cliente não encontrada.");

        return new ClientCompanyDto(
            c.Id,
            c.LegalName,
            c.TradeName,
            c.Cnpj,
            c.Email,
            c.Phone,
            c.Address,
            c.ResponsibleName,
            c.Notes,
            c.Status,
            c.CreatedAt,
            c.Units.Count
        );
    }

    public async Task<ClientCompanyDto> CreateAsync(CreateClientCompanyRequest request)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        // Validate plan limit before adding
        await _entitlementService.ValidateLimitAsync(tenantId, "max_client_companies");

        var client = new ClientCompany
        {
            TenantId = tenantId,
            LegalName = request.LegalName,
            TradeName = string.IsNullOrWhiteSpace(request.TradeName) ? request.LegalName : request.TradeName,
            Cnpj = request.Cnpj,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            ResponsibleName = request.ResponsibleName,
            Notes = request.Notes,
            Status = CommonStatus.Active
        };

        _context.ClientCompanies.Add(client);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(client.Id);
    }

    public async Task<ClientCompanyDto> UpdateAsync(Guid id, UpdateClientCompanyRequest request)
    {
        var client = await _context.ClientCompanies.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (client == null) throw new KeyNotFoundException("Empresa cliente não encontrada.");

        client.LegalName = request.LegalName;
        client.TradeName = string.IsNullOrWhiteSpace(request.TradeName) ? request.LegalName : request.TradeName;
        client.Email = request.Email;
        client.Phone = request.Phone;
        client.Address = request.Address;
        client.ResponsibleName = request.ResponsibleName;
        client.Notes = request.Notes;
        client.Status = request.Status;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(client.Id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var client = await _context.ClientCompanies
            .Include(c => c.Units)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (client == null) throw new KeyNotFoundException("Empresa cliente não encontrada.");

        client.IsDeleted = true;
        client.DeletedAt = DateTime.UtcNow;
        client.Status = CommonStatus.Inactive;

        // Soft delete associated units as well
        foreach (var unit in client.Units.Where(u => !u.IsDeleted))
        {
            unit.IsDeleted = true;
            unit.DeletedAt = DateTime.UtcNow;
            unit.Status = CommonStatus.Inactive;
        }

        await _context.SaveChangesAsync();
    }
}
