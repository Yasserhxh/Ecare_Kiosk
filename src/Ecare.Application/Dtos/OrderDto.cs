using Ecare.Domain;

namespace Ecare.Application.Dtos;
public record OrderDto(string Number, string? Destination, string? DeliveryMode, string? TruckPlate, OrderStatus Status);
