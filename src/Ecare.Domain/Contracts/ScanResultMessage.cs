using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.Contracts
{
    public sealed record ScanResultMessage(
        int DriverId,
        string Plate,
        string CarteSLV,
        string? ClientName,
        bool? SapOk,
        OrderSummary? Order);

    public sealed record OrderSummary(
        string Number,
        string? TruckPlate,
        IReadOnlyList<OrderItemSummary> Items);

    public sealed record OrderItemSummary(
        int ProductId,
        string? ProductName,
        decimal Quantity,
        string? Unite);
}
