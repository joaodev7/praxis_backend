using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record ChecklistDto(
    Guid Id,
    string Name,
    string Description,
    CommonStatus Status,
    DateTime CreatedAt,
    List<ChecklistItemDto> Items
);

public record ChecklistItemDto(
    Guid Id,
    Guid ChecklistId,
    string Category,
    string Description,
    int Order,
    bool Required,
    CommonStatus Status
);

public record CreateChecklistRequest(
    string Name,
    string Description,
    List<CreateChecklistItemRequest> Items
);

public record CreateChecklistItemRequest(
    string Category,
    string Description,
    int Order,
    bool Required
);

public record UpdateChecklistRequest(
    string Name,
    string Description,
    CommonStatus Status,
    List<UpdateChecklistItemRequest>? Items
);

public record UpdateChecklistItemRequest(
    Guid? Id,
    string Category,
    string Description,
    int Order,
    bool Required,
    CommonStatus Status
);
