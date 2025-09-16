namespace Ecare.Domain.Entities;
public class BonLivraison
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string BlNumber { get; set; } = default!;
    public int NetKg { get; set; }
    public DateTime IssuedAt { get; set; }
}
