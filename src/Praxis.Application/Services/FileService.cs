using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Application.Services;

public class FileService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly ILogger<FileService> _logger;

    private const int DefaultExpirationMinutes = 15;
    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const long MaxPdfSizeBytes = 10 * 1024 * 1024;  // 10 MB

    private static readonly Dictionary<string, string[]> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "image/jpeg", new[] { ".jpg", ".jpeg" } },
        { "image/png", new[] { ".png" } },
        { "image/webp", new[] { ".webp" } },
        { "application/pdf", new[] { ".pdf" } }
    };

    public FileService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IFileStorageService storage,
        ILogger<FileService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _storage = storage;
        _logger = logger;
    }

    public async Task<GenerateUploadUrlResponse> GenerateUploadUrlAsync(
        GenerateUploadUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado na sessão.");

        // 1. Sanitize file name and validate basic fields
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ArgumentException("Nome do arquivo é obrigatório.", nameof(request.FileName));

        var sanitizedFileName = Path.GetFileName(request.FileName.Trim());
        if (string.IsNullOrWhiteSpace(sanitizedFileName))
            throw new ArgumentException("Nome do arquivo inválido.", nameof(request.FileName));

        var extension = Path.GetExtension(sanitizedFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("O arquivo deve conter uma extensão válida (.jpg, .png, .webp, .pdf).", nameof(request.FileName));

        // 2. Validate MIME Type and Extension consistency
        var cleanContentType = request.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedMimeTypes.TryGetValue(cleanContentType, out var allowedExtensions) || !allowedExtensions.Contains(extension))
        {
            throw new ArgumentException($"Tipo de arquivo não permitido ({cleanContentType} com extensão {extension}). Tipos aceitos: JPEG, PNG, WEBP e PDF.");
        }

        // 3. Validate Size Limit
        if (request.Size <= 0)
            throw new ArgumentException("Tamanho do arquivo deve ser maior que zero.", nameof(request.Size));

        var isImage = cleanContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        var maxSize = isImage ? MaxImageSizeBytes : MaxPdfSizeBytes;
        var maxMb = maxSize / (1024 * 1024);

        if (request.Size > maxSize)
        {
            throw new ArgumentException($"O tamanho do arquivo ({request.Size / 1024 / 1024:F1} MB) excede o limite máximo permitido de {maxMb} MB.");
        }

        // 4. Validate Related Client (if provided)
        if (request.ClientId.HasValue)
        {
            var clientExists = await _context.ClientCompanies
                .AnyAsync(c => c.Id == request.ClientId.Value && !c.IsDeleted, cancellationToken);

            if (!clientExists)
                throw new KeyNotFoundException("Cliente especificado não foi encontrado ou não pertence a esta organização.");
        }

        // 5. Generate secure, unique ObjectKey
        var year = DateTime.UtcNow.Year.ToString();
        var uniqueFileId = Guid.NewGuid();
        var objectKey = BuildObjectKey(tenantId, request.ClientId, request.Category, year, uniqueFileId, extension);

        // 6. Register Metadata in PostgreSQL
        var fileRecord = new StoredFile
        {
            Id = uniqueFileId,
            TenantId = tenantId,
            UploadedByUserId = _currentUser.UserId,
            ClientId = request.ClientId,
            OriginalFileName = sanitizedFileName,
            ObjectKey = objectKey,
            ContentType = cleanContentType,
            Size = request.Size,
            Category = request.Category,
            Status = FileStatus.Pending,
            IsDeleted = false
        };

        _context.Files.Add(fileRecord);
        await _context.SaveChangesAsync(cancellationToken);

        // 7. Generate presigned PUT URL
        var expiration = TimeSpan.FromMinutes(DefaultExpirationMinutes);
        var presignedUrl = await _storage.GenerateUploadUrlAsync(objectKey, cleanContentType, expiration, cancellationToken);

        _logger.LogInformation("Upload URL gerada para o arquivo {FileId} (Tenant: {TenantId}, Category: {Category}, Size: {Size} bytes)",
            uniqueFileId, tenantId, request.Category, request.Size);

        return new GenerateUploadUrlResponse(
            uniqueFileId,
            objectKey,
            presignedUrl,
            (int)expiration.TotalSeconds
        );
    }

    public async Task<CompleteUploadResponse> CompleteUploadAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var file = await _context.Files
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted, cancellationToken);

        if (file == null)
            throw new KeyNotFoundException("Arquivo não encontrado.");

        if (file.Status == FileStatus.Uploaded)
        {
            return new CompleteUploadResponse(
                file.Id,
                file.Status,
                file.OriginalFileName,
                file.ContentType,
                file.Size,
                file.UploadedAt,
                "Arquivo já havia sido confirmado anteriormente."
            );
        }

        // Verify object existence in Cloudflare R2
        var existsInR2 = await _storage.ExistsAsync(file.ObjectKey, cancellationToken);
        if (!existsInR2)
        {
            _logger.LogWarning("Tentativa de confirmação de upload com falha. Objeto {ObjectKey} não encontrado no R2.", file.ObjectKey);
            throw new InvalidOperationException("O arquivo ainda não foi enviado para o Cloudflare R2 ou o upload direto pelo frontend não foi concluído.");
        }

        file.Status = FileStatus.Uploaded;
        file.UploadedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Upload concluído e confirmado com sucesso para o arquivo {FileId} (ObjectKey: {ObjectKey})",
            file.Id, file.ObjectKey);

        return new CompleteUploadResponse(
            file.Id,
            file.Status,
            file.OriginalFileName,
            file.ContentType,
            file.Size,
            file.UploadedAt,
            "Upload confirmado com sucesso."
        );
    }

    public async Task<FileDownloadUrlResponse> GetDownloadUrlAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var file = await _context.Files
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted, cancellationToken);

        if (file == null)
            throw new KeyNotFoundException("Arquivo não encontrado.");

        if (file.Status != FileStatus.Uploaded)
            throw new InvalidOperationException("Arquivo não disponível para download pois o upload ainda não foi finalizado.");

        var expiration = TimeSpan.FromMinutes(DefaultExpirationMinutes);
        var downloadUrl = await _storage.GenerateDownloadUrlAsync(file.ObjectKey, expiration, cancellationToken);

        _logger.LogInformation("Download URL gerada com sucesso para o arquivo {FileId} pelo usuário {UserId}",
            file.Id, _currentUser.UserId);

        return new FileDownloadUrlResponse(
            file.Id,
            downloadUrl,
            (int)expiration.TotalSeconds,
            file.OriginalFileName,
            file.ContentType
        );
    }

    public async Task<bool> DeleteFileAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado.");

        var file = await _context.Files
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted, cancellationToken);

        if (file == null)
            throw new KeyNotFoundException("Arquivo não encontrado.");

        // Remove from R2
        await _storage.DeleteAsync(file.ObjectKey, cancellationToken);

        // Soft delete in PostgreSQL
        file.IsDeleted = true;
        file.DeletedAt = DateTime.UtcNow;
        file.Status = FileStatus.Deleted;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Arquivo {FileId} (ObjectKey: {ObjectKey}) excluído pelo usuário {UserId}",
            file.Id, file.ObjectKey, _currentUser.UserId);

        return true;
    }

    public async Task<FileDto> GetByIdAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var file = await _context.Files
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted, cancellationToken);

        if (file == null)
            throw new KeyNotFoundException("Arquivo não encontrado.");

        return MapToFileDto(file);
    }

    public async Task<IEnumerable<FileDto>> ListByClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        var files = await _context.Files
            .Where(f => f.ClientId == clientId && !f.IsDeleted && f.Status == FileStatus.Uploaded)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        return files.Select(MapToFileDto);
    }

    private static string BuildObjectKey(
        Guid tenantId,
        Guid? clientId,
        FileCategory category,
        string year,
        Guid fileId,
        string extension)
    {
        var categoryFolder = category switch
        {
            FileCategory.ClientPhoto => "photos",
            FileCategory.Report => "reports",
            FileCategory.Evidence => "evidences",
            FileCategory.Document => "documents",
            _ => "files"
        };

        var clientPart = clientId.HasValue
            ? $"clients/{clientId.Value}"
            : "general";

        return $"tenants/{tenantId}/{clientPart}/{categoryFolder}/{year}/{fileId}{extension}";
    }

    private static FileDto MapToFileDto(StoredFile file)
    {
        return new FileDto(
            file.Id,
            file.OriginalFileName,
            file.ObjectKey,
            file.ContentType,
            file.Size,
            file.Category,
            file.Status,
            file.CreatedAt,
            file.UploadedAt,
            file.ClientId,
            file.UploadedByUserId
        );
    }
}
