using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Application.Services;

public class AuthService
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICurrentUserService _currentUser;

    public AuthService(IApplicationDbContext context, IJwtTokenService jwtTokenService, ICurrentUserService currentUser)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
        _currentUser = currentUser;
    }

    public async Task<LoginResponse> RegisterTenantAsync(RegisterTenantRequest request)
    {
        var existingEmail = await _context.Users.AnyAsync(u => u.Email.ToLower() == request.AdminEmail.ToLower());
        if (existingEmail)
            throw new InvalidOperationException("E-mail já cadastrado.");

        var tenant = new Tenant
        {
            Name = request.TenantName,
            LegalName = request.LegalName,
            Cnpj = request.Cnpj,
            Email = request.AdminEmail,
            Phone = request.Phone ?? string.Empty,
            Status = TenantStatus.Active
        };

        _context.Tenants.Add(tenant);

        var adminUser = new User
        {
            TenantId = tenant.Id,
            Name = request.AdminName,
            Email = request.AdminEmail.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
            Role = UserRole.TenantAdmin,
            Status = UserStatus.Active
        };

        _context.Users.Add(adminUser);

        // Create default initial checklist for the tenant
        var defaultChecklist = new Checklist
        {
            TenantId = tenant.Id,
            Name = "Checklist Padrão de Boas Práticas (RDC 216)",
            Description = "Checklist base de higiene, armazenamento, manipulação e instalações.",
            Status = CommonStatus.Active,
            Items = new List<ChecklistItem>
            {
                new() { Category = "Higiene Pessoal", Description = "Uniformes limpos e em bom estado", Order = 1, Required = true },
                new() { Category = "Higiene Pessoal", Description = "Lavagem correta e higienização das mãos", Order = 2, Required = true },
                new() { Category = "Armazenamento", Description = "Identificação e prazo de validade de todos os produtos", Order = 3, Required = true },
                new() { Category = "Armazenamento", Description = "Controle e registro diário de temperatura de refrigeradores", Order = 4, Required = true },
                new() { Category = "Instalações & Equipamentos", Description = "Equipamentos e bancadas devidamente higienizados e sanitizados", Order = 5, Required = true },
                new() { Category = "Instalações & Equipamentos", Description = "Telas milimetradas intactas e proteção contra pragas", Order = 6, Required = true },
                new() { Category = "Manipulação", Description = "Separação de alimentos crus e cozidos (prevenção de contaminação cruzada)", Order = 7, Required = true }
            }
        };
        _context.Checklists.Add(defaultChecklist);

        await _context.SaveChangesAsync();

        var token = _jwtTokenService.GenerateToken(adminUser);

        return new LoginResponse(
            token,
            new UserDto(adminUser.Id, tenant.Id, adminUser.Name, adminUser.Email, adminUser.Role, adminUser.Status, null),
            new TenantDto(tenant.Id, tenant.Name, tenant.LegalName, tenant.Cnpj, tenant.Email, tenant.Phone, tenant.Status, tenant.CreatedAt)
        );
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Tenant)
            .Include(u => u.NutritionistProfile)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower() && !u.IsDeleted);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Credenciais inválidas.");

        if (user.Status != UserStatus.Active)
            throw new UnauthorizedAccessException("Usuário inativo ou bloqueado.");

        if (user.Tenant == null || user.Tenant.Status != TenantStatus.Active)
            throw new UnauthorizedAccessException("Empresa de nutrição inativa.");

        var token = _jwtTokenService.GenerateToken(user);

        // Record Login AuditLog
        _context.AuditLogs.Add(new AuditLog
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            Action = "LOGIN",
            Entity = "User",
            EntityId = user.Id.ToString(),
            Metadata = $"Login bem-sucedido do usuário {user.Email}"
        });
        await _context.SaveChangesAsync();

        return new LoginResponse(
            token,
            new UserDto(user.Id, user.TenantId, user.Name, user.Email, user.Role, user.Status, user.NutritionistProfile?.Id),
            new TenantDto(user.Tenant.Id, user.Tenant.Name, user.Tenant.LegalName, user.Tenant.Cnpj, user.Tenant.Email, user.Tenant.Phone, user.Tenant.Status, user.Tenant.CreatedAt)
        );
    }

    public async Task<UserDto> GetCurrentUserAsync()
    {
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("Não autenticado.");

        var user = await _context.Users
            .Include(u => u.NutritionistProfile)
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value);

        if (user == null)
            throw new KeyNotFoundException("Usuário não encontrado.");

        return new UserDto(user.Id, user.TenantId, user.Name, user.Email, user.Role, user.Status, user.NutritionistProfile?.Id);
    }

    public async Task<object> ExportUserDataAsync()
    {
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("Não autenticado.");

        var userId = _currentUser.UserId.Value;
        var user = await _context.Users
            .Include(u => u.Tenant)
            .Include(u => u.NutritionistProfile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new KeyNotFoundException("Usuário não encontrado.");

        var nutritionistId = user.NutritionistProfile?.Id;

        var arts = await _context.ARTs
            .Where(a => (nutritionistId.HasValue && a.NutritionistId == nutritionistId.Value) || a.TenantId == user.TenantId)
            .Select(a => new
            {
                a.Id,
                a.Number,
                a.StartDate,
                a.EndDate,
                status = a.Status.ToString(),
                a.Notes,
                a.CreatedAt
            })
            .ToListAsync();

        var visits = await _context.Visits
            .Where(v => (nutritionistId.HasValue && v.NutritionistId == nutritionistId.Value) || v.TenantId == user.TenantId)
            .Select(v => new
            {
                v.Id,
                v.ScheduledAt,
                v.StartedAt,
                v.FinishedAt,
                status = v.Status.ToString(),
                v.Notes,
                v.CreatedAt
            })
            .ToListAsync();

        return new
        {
            meta = new
            {
                exportDate = DateTime.UtcNow,
                purpose = "Relatório de Portabilidade de Dados Pessoais - LGPD (Art. 18, Inciso V da Lei nº 13.709/2018)",
                system = "PRAXIS - Gestão de Responsabilidade Técnica"
            },
            user = new
            {
                user.Id,
                user.Name,
                user.Email,
                role = user.Role.ToString(),
                status = user.Status.ToString(),
                user.CreatedAt,
                user.UpdatedAt
            },
            nutritionistProfile = user.NutritionistProfile == null ? null : new
            {
                user.NutritionistProfile.Id,
                user.NutritionistProfile.Crn,
                user.NutritionistProfile.Phone,
                status = user.NutritionistProfile.Status.ToString()
            },
            tenant = user.Tenant == null ? null : new
            {
                user.Tenant.Id,
                user.Tenant.Name,
                user.Tenant.LegalName,
                user.Tenant.Cnpj,
                user.Tenant.Email,
                user.Tenant.Phone,
                status = user.Tenant.Status.ToString(),
                user.Tenant.CreatedAt
            },
            associatedARTs = arts,
            associatedVisits = visits
        };
    }
}
