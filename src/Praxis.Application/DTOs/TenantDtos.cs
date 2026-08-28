using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record TenantDto(
    Guid Id,
    string Name,
    string LegalName,
    string Cnpj,
    string Email,
    string Phone,
    TenantStatus Status,
    DateTime CreatedAt
);

public record UpdateTenantRequest(
    string Name,
    string LegalName,
    string Email,
    string Phone
);
