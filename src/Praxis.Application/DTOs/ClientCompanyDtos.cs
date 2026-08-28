using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record ClientCompanyDto(
    Guid Id,
    string LegalName,
    string TradeName,
    string Cnpj,
    string Email,
    string Phone,
    string? Address,
    string? ResponsibleName,
    string? Notes,
    CommonStatus Status,
    DateTime CreatedAt,
    int UnitsCount
);

public record CreateClientCompanyRequest(
    string LegalName,
    string TradeName,
    string Cnpj,
    string Email,
    string Phone,
    string? Address,
    string? ResponsibleName,
    string? Notes
);

public record UpdateClientCompanyRequest(
    string LegalName,
    string TradeName,
    string Email,
    string Phone,
    string? Address,
    string? ResponsibleName,
    string? Notes,
    CommonStatus Status
);
