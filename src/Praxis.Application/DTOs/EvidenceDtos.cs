using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record EvidenceDto(
    Guid Id,
    Guid NonConformityId,
    EvidenceType Type,
    string Url,
    string Description,
    DateTime CreatedAt,
    Guid? UploadedByUserId
);

public record CreateEvidenceRequest(
    Guid NonConformityId,
    EvidenceType Type,
    string Url,
    string Description
);
