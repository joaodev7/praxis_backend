using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record GenerateUploadUrlRequest(
    string FileName,
    string ContentType,
    long Size,
    FileCategory Category = FileCategory.Other,
    Guid? ClientId = null
);

public record GenerateUploadUrlResponse(
    Guid FileId,
    string ObjectKey,
    string UploadUrl,
    int ExpiresIn
);

public record FileDto(
    Guid Id,
    string OriginalFileName,
    string ObjectKey,
    string ContentType,
    long Size,
    FileCategory Category,
    FileStatus Status,
    DateTime CreatedAt,
    DateTime? UploadedAt,
    Guid? ClientId,
    Guid? UploadedByUserId
);

public record FileDownloadUrlResponse(
    Guid FileId,
    string DownloadUrl,
    int ExpiresIn,
    string FileName,
    string ContentType
);

public record CompleteUploadResponse(
    Guid FileId,
    FileStatus Status,
    string OriginalFileName,
    string ContentType,
    long Size,
    DateTime? UploadedAt,
    string Message
);
