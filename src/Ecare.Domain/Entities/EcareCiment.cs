namespace Ecare.Domain.Entities;

public class EcareCiment
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? ImageUrl { get; set; }
    public string? Details { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
}
