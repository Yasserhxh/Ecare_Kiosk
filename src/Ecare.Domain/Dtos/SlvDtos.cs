using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.Dtos
{
    public class SlvDtos
    {
        public sealed record ScanBySlvVm(int DriverId, string Plate, string CarteSLV, string? ClientName, bool? SapOk, OrderDto? Order);

        public record OrderDto(string Number, string? Destination, string? DeliveryMode, string? TruckPlate, OrderStatus Status, IEnumerable<OrderItemDto>? Items = null);
        public record OrderItemDto(int ProductId, string? ProductName, decimal Quantity, string? Unite);

    }
}
