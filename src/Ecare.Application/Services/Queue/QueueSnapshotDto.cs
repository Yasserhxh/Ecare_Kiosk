namespace Ecare.Application.Services;

public sealed record QueueItemDto(
    string? Matricule,
    string? Qualite1,
    Domain.ValueObjects.QueueStatus Status,
    DateTime CreatedAt,
    bool IsPined,
    DateTime? PinedAt);

public sealed record QueueGroupDto(
    string Name,
    IReadOnlyList<QueueItemDto> Items);
