namespace Praxis.Application.DTOs;

public sealed record ProfileResponse(
    Guid Id,
    string Name,
    string Email,
    DateOnly? DateOfBirth,
    string? ProfilePhotoUrl
);

public sealed record UpdateProfileRequest(
    string Name,
    DateOnly? DateOfBirth
);

public sealed record ProfilePhotoUploadResponse(
    string ProfilePhotoUrl,
    string Message
);
