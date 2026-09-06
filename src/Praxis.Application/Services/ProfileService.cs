using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;

namespace Praxis.Application.Services;

public class ProfileService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly ILogger<ProfileService> _logger;

    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly Dictionary<string, string[]> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "image/jpeg", new[] { ".jpg", ".jpeg" } },
        { "image/png", new[] { ".png" } },
        { "image/webp", new[] { ".webp" } }
    };

    public ProfileService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IFileStorageService storage,
        ILogger<ProfileService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _storage = storage;
        _logger = logger;
    }

    public async Task<ProfileResponse> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user == null)
            throw new KeyNotFoundException("Perfil de usuário não encontrado.");

        var photoUrl = await ResolvePhotoUrlAsync(user, cancellationToken);

        return new ProfileResponse(
            user.Id,
            user.Name,
            user.Email,
            user.DateOfBirth,
            photoUrl
        );
    }

    public async Task<ProfileResponse> UpdateProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("O nome é obrigatório e não pode ser vazio.");

        var trimmedName = request.Name.Trim();
        if (trimmedName.Length < 2)
            throw new ArgumentException("O nome deve ter no mínimo 2 caracteres.");

        if (trimmedName.Length > 150)
            throw new ArgumentException("O nome deve ter no máximo 150 caracteres.");

        if (request.DateOfBirth.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (request.DateOfBirth.Value > today)
                throw new ArgumentException("A data de nascimento informada é inválida.");

            if (request.DateOfBirth.Value.Year < 1900)
                throw new ArgumentException("A data de nascimento informada é inválida.");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user == null)
            throw new KeyNotFoundException("Perfil de usuário não encontrado.");

        user.Name = trimmedName;
        user.DateOfBirth = request.DateOfBirth;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Profile updated successfully for authenticated user.");

        var photoUrl = await ResolvePhotoUrlAsync(user, cancellationToken);

        return new ProfileResponse(
            user.Id,
            user.Name,
            user.Email,
            user.DateOfBirth,
            photoUrl
        );
    }

    public async Task<ProfilePhotoUploadResponse> UploadPhotoAsync(
        Stream stream,
        string fileName,
        string contentType,
        long size,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant não identificado na sessão.");

        if (stream == null || size <= 0)
            throw new ArgumentException("O arquivo de imagem não pode ser vazio.");

        if (size > MaxImageSizeBytes)
            throw new ArgumentException("A imagem deve possuir no máximo 5 MB.");

        var sanitizedFileName = Path.GetFileName(fileName ?? string.Empty).Trim();
        var extension = Path.GetExtension(sanitizedFileName).ToLowerInvariant();
        var cleanContentType = contentType?.Trim().ToLowerInvariant() ?? string.Empty;

        if (!AllowedImageTypes.TryGetValue(cleanContentType, out var allowedExtensions) ||
            !allowedExtensions.Contains(extension))
        {
            throw new ArgumentException("A imagem enviada não possui um formato válido.");
        }

        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);

        if (memoryStream.Length == 0)
            throw new ArgumentException("O arquivo de imagem não pode ser vazio.");

        if (memoryStream.Length > MaxImageSizeBytes)
            throw new ArgumentException("A imagem deve possuir no máximo 5 MB.");

        memoryStream.Position = 0;
        var header = new byte[12];
        var bytesRead = await memoryStream.ReadAsync(header, 0, header.Length, cancellationToken);
        memoryStream.Position = 0;

        if (!IsValidImageHeader(header, bytesRead, cleanContentType))
        {
            throw new ArgumentException("A imagem enviada não possui um formato válido.");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user == null)
            throw new KeyNotFoundException("Perfil de usuário não encontrado.");

        var key = $"profiles/{tenantId}/{userId}/avatar.webp";

        // Upload new image to R2
        await _storage.UploadAsync(memoryStream, key, "image/webp", cancellationToken);

        var rawUrl = await _storage.GetFileUrlAsync(key, cancellationToken);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var separator = rawUrl.Contains('?') ? "&" : "?";
        var finalPhotoUrl = $"{rawUrl}{separator}v={timestamp}";

        var oldKey = user.ProfilePhotoKey;

        user.ProfilePhotoKey = key;
        user.ProfilePhotoUrl = finalPhotoUrl;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // If previous key existed and differed, clean it up
        if (!string.IsNullOrWhiteSpace(oldKey) && !string.Equals(oldKey, key, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _storage.DeleteAsync(oldKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao remover foto anterior no R2.");
            }
        }

        _logger.LogInformation("Profile photo upload completed for authenticated user.");

        return new ProfilePhotoUploadResponse(
            finalPhotoUrl,
            "Foto de perfil atualizada com sucesso."
        );
    }

    public async Task DeletePhotoAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user == null)
            throw new KeyNotFoundException("Perfil de usuário não encontrado.");

        if (!string.IsNullOrWhiteSpace(user.ProfilePhotoKey))
        {
            await _storage.DeleteAsync(user.ProfilePhotoKey, cancellationToken);
            user.ProfilePhotoKey = null;
            user.ProfilePhotoUrl = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Profile photo deleted for authenticated user.");
        }
    }

    private async Task<string?> ResolvePhotoUrlAsync(User user, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(user.ProfilePhotoKey))
        {
            var rawUrl = await _storage.GetFileUrlAsync(user.ProfilePhotoKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(rawUrl))
            {
                var separator = rawUrl.Contains('?') ? "&" : "?";
                var version = user.UpdatedAt?.Ticks ?? user.CreatedAt.Ticks;
                return $"{rawUrl}{separator}v={version}";
            }
        }

        return user.ProfilePhotoUrl;
    }

    private static bool IsValidImageHeader(byte[] header, int bytesRead, string contentType)
    {
        if (bytesRead < 4)
            return false;

        // JPEG: FF D8 FF
        if (contentType == "image/jpeg")
        {
            return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (contentType == "image/png")
        {
            return bytesRead >= 8 &&
                   header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                   header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
        }

        // WebP: RIFF .... WEBP
        if (contentType == "image/webp")
        {
            return bytesRead >= 12 &&
                   header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 && // RIFF
                   header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50; // WEBP
        }

        return false;
    }
}
