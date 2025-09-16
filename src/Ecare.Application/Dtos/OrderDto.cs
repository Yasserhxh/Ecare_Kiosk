using Ecare.Domain;

namespace Ecare.Application.Dtos;
public record OrderDto(string Number, string ProductName, string Unit, decimal Quantity, OrderStatus Status);
