using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record RegisterTenantRequest(
    string TenantName,
    string LegalName,
    string Cnpj,
    string AdminName,
    string AdminEmail,
    string AdminPassword,
    string? Phone
);

public record LoginRequest(
    string Email,
    string Password
);

public record LoginResponse(
    string Token,
    UserDto User,
    TenantDto Tenant
);

public record UserDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Email,
    UserRole Role,
    UserStatus Status,
    Guid? NutritionistId
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);
