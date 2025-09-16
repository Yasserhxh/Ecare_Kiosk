namespace Ecare.Domain.Entities;
using Ecare.Domain;

public class Order
{
    public Guid Id { get; set; }
    public string Number { get; set; } = default!;
    public Guid DriverId { get; set; }
    public Guid ClientId { get; set; }
    public int ProductId { get; set; }
    public ProductType ProductType { get; set; }
    public string ProductName { get; set; } = default!;
    public string Unit { get; set; } = default!; // Tonnes/Sac/Camion
    public decimal Quantity { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Cree;
    public DateTime CreatedAt { get; set; }
}
