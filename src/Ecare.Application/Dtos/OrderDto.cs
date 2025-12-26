using Ecare.Domain;

namespace Ecare.Application.Dtos;
public record OrderDto(int OrderId,string Number, string? Destination, string? DeliveryMode, string? TruckPlate, OrderStatus Status, IEnumerable<OrderItemDto>? Items = null);
public record OrderItemDto(int ProductId, string? ProductName, decimal Quantity, string? Unite,string ImageUrl);
