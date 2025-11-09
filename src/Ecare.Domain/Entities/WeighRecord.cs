namespace Ecare.Domain.Entities;
public class WeighRecord
{
    public Guid Id { get; set; }
    public int OrderId { get; set; }
    public int Stage { get; set; } // 1 = PaB1, 2 = PaB2
    public int GrossKg { get; set; }
    public int TareKg { get; set; }
    public int NetKg { get; set; }
    public DateTime TakenAt { get; set; }
}
