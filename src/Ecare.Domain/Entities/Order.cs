namespace Ecare.Domain.Entities;
using Ecare.Domain;

public class Order
{
    public Guid Id { get; set; }
    public Guid? ShippingId { get; set; }
    public string Number { get; set; } = default!;
    public DateTime? OrderedAt { get; set; }
    public string? Destination { get; set; }
    public string? DeliveryMode { get; set; }
    public string? LoadingLocation { get; set; }
    public string? LoadingMethod { get; set; }
    public TimeSpan? PickupTime { get; set; }
    public TimeSpan? DeliveryTime { get; set; }
    public string? SlvCard { get; set; }
    public string? DriverLastName { get; set; }
    public string? DriverFirstName { get; set; }
    public string? DriverLicense { get; set; }
    public string? TruckPlate { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Cree;
    public Guid? UserId { get; set; }
    public decimal? TargetQuantityTons { get; set; }
}
