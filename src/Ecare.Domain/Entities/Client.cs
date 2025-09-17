namespace Ecare.Domain.Entities;
public class Client
{
    public Guid Id { get; init; }
    public string Name { get; set; } = default!;
    public string? SapCode { get; set; }
    public bool SapOk => !string.IsNullOrWhiteSpace(SapCode);
}
