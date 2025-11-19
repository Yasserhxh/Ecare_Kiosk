namespace Ecare.Application.Services
{
    public sealed record QueueItemDto(
        string? Matricule,
        string? Qualite1,
        Domain.ValueObjects.QueueStatus Status,
        string? Nom_Chaufeur,
        DateTime CreatedAt,
        bool IsPined,
        DateTime? PinedAt,
        int? CarteSlv  
    );

    public sealed record QueueGroupDto(
        string Name,
        IReadOnlyList<QueueItemDto> Items,
        int Capacity);
}
