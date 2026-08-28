using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Application.Services;

public class NutritionistService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IEntitlementService _entitlementService;

    public NutritionistService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IEntitlementService entitlementService)
    {
        _context = context;
        _currentUser = currentUser;
        _entitlementService = entitlementService;
    }

    public async Task<List<NutritionistDto>> GetAllAsync()
    {
        var nutritionists = await _context.Nutritionists
            .Include(n => n.User)
            .Include(n => n.UnitAssignments)
            .Where(n => !n.IsDeleted)
            .OrderBy(n => n.User!.Name)
            .ToListAsync();

        return nutritionists.Select(n => new NutritionistDto(
            n.Id,
            n.UserId,
            n.User?.Name ?? string.Empty,
            n.User?.Email ?? string.Empty,
            n.Crn,
            n.Phone,
            n.Status,
            n.CreatedAt,
            n.UnitAssignments.Select(ua => ua.UnitId).ToList()
        )).ToList();
    }

    public async Task<NutritionistDto> GetByIdAsync(Guid id)
    {
        var n = await _context.Nutritionists
            .Include(n => n.User)
            .Include(n => n.UnitAssignments)
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);

        if (n == null) throw new KeyNotFoundException("Nutricionista não encontrado.");

        return new NutritionistDto(
            n.Id,
            n.UserId,
            n.User?.Name ?? string.Empty,
            n.User?.Email ?? string.Empty,
            n.Crn,
            n.Phone,
            n.Status,
            n.CreatedAt,
            n.UnitAssignments.Select(ua => ua.UnitId).ToList()
        );
    }

    public async Task<NutritionistDto> CreateAsync(CreateNutritionistRequest request)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        // Validate plan limit before adding
        await _entitlementService.ValidateLimitAsync(tenantId, "max_nutritionists");

        var existingUser = await _context.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower());
        if (existingUser)
            throw new InvalidOperationException("E-mail já está em uso.");

        var user = new User
        {
            TenantId = tenantId,
            Name = request.Name,
            Email = request.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Nutritionist,
            Status = UserStatus.Active
        };

        _context.Users.Add(user);

        var nutritionist = new Nutritionist
        {
            TenantId = tenantId,
            UserId = user.Id,
            Crn = request.Crn,
            Phone = request.Phone,
            Status = CommonStatus.Active
        };

        _context.Nutritionists.Add(nutritionist);

        if (request.AssignedUnitIds != null && request.AssignedUnitIds.Any())
        {
            foreach (var unitId in request.AssignedUnitIds)
            {
                _context.NutritionistUnitAssignments.Add(new NutritionistUnitAssignment
                {
                    TenantId = tenantId,
                    NutritionistId = nutritionist.Id,
                    UnitId = unitId
                });
            }
        }

        await _context.SaveChangesAsync();

        return await GetByIdAsync(nutritionist.Id);
    }

    public async Task<NutritionistDto> UpdateAsync(Guid id, UpdateNutritionistRequest request)
    {
        var nutritionist = await _context.Nutritionists
            .Include(n => n.User)
            .Include(n => n.UnitAssignments)
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);

        if (nutritionist == null) throw new KeyNotFoundException("Nutricionista não encontrado.");

        nutritionist.Crn = request.Crn;
        nutritionist.Phone = request.Phone;
        nutritionist.Status = request.Status;
        nutritionist.UpdatedAt = DateTime.UtcNow;

        if (nutritionist.User != null)
        {
            nutritionist.User.Name = request.Name;
            nutritionist.User.UpdatedAt = DateTime.UtcNow;
            nutritionist.User.Status = request.Status == CommonStatus.Active ? UserStatus.Active : UserStatus.Inactive;
        }

        if (request.AssignedUnitIds != null)
        {
            // Remove old assignments
            _context.NutritionistUnitAssignments.RemoveRange(nutritionist.UnitAssignments);

            // Add new assignments
            foreach (var unitId in request.AssignedUnitIds)
            {
                _context.NutritionistUnitAssignments.Add(new NutritionistUnitAssignment
                {
                    TenantId = nutritionist.TenantId,
                    NutritionistId = nutritionist.Id,
                    UnitId = unitId
                });
            }
        }

        await _context.SaveChangesAsync();

        return await GetByIdAsync(nutritionist.Id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var nutritionist = await _context.Nutritionists
            .Include(n => n.User)
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);

        if (nutritionist == null) throw new KeyNotFoundException("Nutricionista não encontrado.");

        nutritionist.IsDeleted = true;
        nutritionist.DeletedAt = DateTime.UtcNow;
        nutritionist.Status = CommonStatus.Inactive;

        if (nutritionist.User != null)
        {
            nutritionist.User.IsDeleted = true;
            nutritionist.User.DeletedAt = DateTime.UtcNow;
            nutritionist.User.Status = UserStatus.Inactive;
        }

        await _context.SaveChangesAsync();
    }
}
