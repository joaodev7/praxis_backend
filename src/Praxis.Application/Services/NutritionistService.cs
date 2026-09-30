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

        var normalizedEmail = request.Email.Trim().ToLower();

        var existingUser = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.NutritionistProfile)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (existingUser != null)
        {
            if (existingUser.TenantId != tenantId)
            {
                throw new InvalidOperationException("Este e-mail já está associado a outra organização no sistema.");
            }

            if (!existingUser.IsDeleted)
            {
                throw new InvalidOperationException("Já existe um usuário ativo cadastrado com este e-mail.");
            }

            // Se o usuário foi soft-deleted na mesma organização, reativamos com os novos dados
            existingUser.IsDeleted = false;
            existingUser.DeletedAt = null;
            existingUser.Name = request.Name.Trim();
            existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            existingUser.Role = UserRole.Nutritionist;
            existingUser.Status = UserStatus.Active;
            existingUser.UpdatedAt = DateTime.UtcNow;

            var existingNutritionist = existingUser.NutritionistProfile
                ?? await _context.Nutritionists.IgnoreQueryFilters().FirstOrDefaultAsync(n => n.UserId == existingUser.Id);

            if (existingNutritionist != null)
            {
                existingNutritionist.IsDeleted = false;
                existingNutritionist.DeletedAt = null;
                existingNutritionist.Crn = request.Crn.Trim();
                existingNutritionist.Phone = request.Phone?.Trim() ?? string.Empty;
                existingNutritionist.Status = CommonStatus.Active;
                existingNutritionist.UpdatedAt = DateTime.UtcNow;

                var existingAssignments = await _context.NutritionistUnitAssignments
                    .IgnoreQueryFilters()
                    .Where(nua => nua.NutritionistId == existingNutritionist.Id)
                    .ToListAsync();
                _context.NutritionistUnitAssignments.RemoveRange(existingAssignments);
            }
            else
            {
                existingNutritionist = new Nutritionist
                {
                    TenantId = tenantId,
                    UserId = existingUser.Id,
                    Crn = request.Crn.Trim(),
                    Phone = request.Phone?.Trim() ?? string.Empty,
                    Status = CommonStatus.Active
                };
                _context.Nutritionists.Add(existingNutritionist);
            }

            if (request.AssignedUnitIds != null && request.AssignedUnitIds.Any())
            {
                foreach (var unitId in request.AssignedUnitIds)
                {
                    var unitExists = await _context.Units.AnyAsync(u => u.Id == unitId && !u.IsDeleted);
                    if (unitExists)
                    {
                        _context.NutritionistUnitAssignments.Add(new NutritionistUnitAssignment
                        {
                            TenantId = tenantId,
                            NutritionistId = existingNutritionist.Id,
                            UnitId = unitId
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            return await GetByIdAsync(existingNutritionist.Id);
        }

        var user = new User
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Nutritionist,
            Status = UserStatus.Active
        };

        _context.Users.Add(user);

        var nutritionist = new Nutritionist
        {
            TenantId = tenantId,
            UserId = user.Id,
            Crn = request.Crn.Trim(),
            Phone = request.Phone?.Trim() ?? string.Empty,
            Status = CommonStatus.Active
        };

        _context.Nutritionists.Add(nutritionist);

        if (request.AssignedUnitIds != null && request.AssignedUnitIds.Any())
        {
            foreach (var unitId in request.AssignedUnitIds)
            {
                var unitExists = await _context.Units.AnyAsync(u => u.Id == unitId && !u.IsDeleted);
                if (unitExists)
                {
                    _context.NutritionistUnitAssignments.Add(new NutritionistUnitAssignment
                    {
                        TenantId = tenantId,
                        NutritionistId = nutritionist.Id,
                        UnitId = unitId
                    });
                }
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

        nutritionist.Crn = request.Crn.Trim();
        nutritionist.Phone = request.Phone?.Trim() ?? string.Empty;
        nutritionist.Status = request.Status;
        nutritionist.UpdatedAt = DateTime.UtcNow;

        if (nutritionist.User != null)
        {
            nutritionist.User.Name = request.Name.Trim();
            nutritionist.User.UpdatedAt = DateTime.UtcNow;
            nutritionist.User.Status = request.Status == CommonStatus.Active ? UserStatus.Active : UserStatus.Inactive;

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var normalizedEmail = request.Email.Trim().ToLower();
                if (!nutritionist.User.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase))
                {
                    var existingEmail = await _context.Users
                        .IgnoreQueryFilters()
                        .AnyAsync(u => u.Email.ToLower() == normalizedEmail && u.Id != nutritionist.UserId);

                    if (existingEmail)
                        throw new InvalidOperationException("E-mail já está em uso por outro usuário.");

                    nutritionist.User.Email = normalizedEmail;
                }
            }
        }

        if (request.AssignedUnitIds != null)
        {
            // Remove old assignments
            _context.NutritionistUnitAssignments.RemoveRange(nutritionist.UnitAssignments);

            // Add new assignments
            foreach (var unitId in request.AssignedUnitIds)
            {
                var unitExists = await _context.Units.AnyAsync(u => u.Id == unitId && !u.IsDeleted);
                if (unitExists)
                {
                    _context.NutritionistUnitAssignments.Add(new NutritionistUnitAssignment
                    {
                        TenantId = nutritionist.TenantId,
                        NutritionistId = nutritionist.Id,
                        UnitId = unitId
                    });
                }
            }
        }

        await _context.SaveChangesAsync();

        return await GetByIdAsync(nutritionist.Id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var nutritionist = await _context.Nutritionists
            .Include(n => n.User)
            .Include(n => n.UnitAssignments)
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
