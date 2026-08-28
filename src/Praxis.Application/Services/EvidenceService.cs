using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Application.Services;

public class EvidenceService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;

    public EvidenceService(IApplicationDbContext context, ICurrentUserService currentUser, IFileStorageService storage)
    {
        _context = context;
        _currentUser = currentUser;
        _storage = storage;
    }

    public async Task<EvidenceDto> CreateAsync(CreateEvidenceRequest request)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var nc = await _context.NonConformities.FirstOrDefaultAsync(n => n.Id == request.NonConformityId && !n.IsDeleted);
        if (nc == null) throw new KeyNotFoundException("Não conformidade não encontrada.");

        var evidence = new Evidence
        {
            TenantId = tenantId,
            NonConformityId = request.NonConformityId,
            Type = request.Type,
            Url = request.Url,
            Description = request.Description,
            UploadedByUserId = _currentUser.UserId
        };

        _context.Evidences.Add(evidence);
        await _context.SaveChangesAsync();

        return new EvidenceDto(evidence.Id, evidence.NonConformityId, evidence.Type, evidence.Url, evidence.Description, evidence.CreatedAt, evidence.UploadedByUserId);
    }

    public async Task<string> UploadFileAsync(Stream stream, string fileName, string contentType)
    {
        return await _storage.SaveFileAsync(stream, fileName, contentType);
    }
}
