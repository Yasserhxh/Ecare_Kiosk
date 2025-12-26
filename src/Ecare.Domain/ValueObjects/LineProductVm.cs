namespace Ecare.Domain.ValueObjects
{
    public sealed record LineProductVm(
        int Id,
        string Name,
        string Type,
        bool Actif);
}
