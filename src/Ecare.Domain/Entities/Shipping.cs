using System;

namespace Ecare.Domain.Entities;

public class Shipping
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? Destination { get; set; }
    public string? DeliveryMode { get; set; }
    public DateTime? DeliveryTime { get; set; }
    public string? NumeroCommande { get; set; }
    public string? LoadingMethod { get; set; }
    public string? LoadingLocation { get; set; }
    public DateTime? PickupTime { get; set; }
    public string? SlvCard { get; set; }
    public Guid? DriverId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Commentaire { get; set; }
}
