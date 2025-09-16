namespace Ecare.Domain.Entities;
using Ecare.Domain;

public class Line
{
    public Guid Id { get; init; }
    public string Name { get; set; } = default!;
    public ProductType Type { get; set; }
    public int Capacity { get; set; }
    public int Occupied { get; set; }
    public LineStatus Status { get; set; }
}
