using Dapper;
using Ecare.Application.Dtos;
using Ecare.Domain;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Ecare.Application.Queries.GetLoadingData;


public sealed class GetLoadingDataHandler : IRequestHandler<GetLoadingDataQuery, Result<GetLoadingDataVm>>
{
    private readonly string _connString;

    public GetLoadingDataHandler(IConfiguration cfg)
    {
        _connString = cfg.GetConnectionString("SqlServer")
                      ?? throw new InvalidOperationException("Missing connection string SqlServer");
    }

    public async Task<Result<GetLoadingDataVm>> Handle(GetLoadingDataQuery request, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connString);
        await conn.OpenAsync(ct);

        // ───────────────────────────────────────────────────────────────────────────────
        // CALL STORED PROCEDURE
        // ───────────────────────────────────────────────────────────────────────────────
        var rows = await conn.QueryAsync<LoadingRow>(
            "sp_GetLoadingDataByRfid",
            new { Rfid = request.Rfid },
            commandType: System.Data.CommandType.StoredProcedure);

        if (!rows.Any())
            return Result<GetLoadingDataVm>.Fail("NO_DATA");

        var first = rows.First();

        // ───────────────────────────────────────────────────────────────────────────────
        // PARSE ENUM SAFELY
        // ───────────────────────────────────────────────────────────────────────────────
        OrderStatus parsedStatus;
        var rawStatus = first.OrderStatus?.ToString()?.Trim() ?? "";

        // remove spaces "En transit" -> "Entransit"
        string normalized = rawStatus.Replace(" ", "");

        if (!Enum.TryParse<OrderStatus>(normalized, true, out parsedStatus))
        {
            parsedStatus = OrderStatus.EnTraitement;   // fallback
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // MAP ORDER ITEMS
        // ───────────────────────────────────────────────────────────────────────────────
        var items = rows
            .Where(r => r.ProductId != null)
            .Select(r => new OrderItemDto(
                ProductId: r.ProductId!.Value,
                ProductName: r.ProductName,
                Quantity: r.Quantity ?? 0,
                Unite: r.Unite,
                ImageUrl: r.ImageUrl))
            .ToList();

        // ───────────────────────────────────────────────────────────────────────────────
        // BUILD ORDER DTO
        // ───────────────────────────────────────────────────────────────────────────────
        OrderDto? order = null;

        if (first.OrderId != null)
        {
            order = new OrderDto(
                OrderId: first.OrderId.Value,
                Number: first.OrderNumber,
                Destination: first.Destination,
                DeliveryMode: first.DeliveryMode,
                TruckPlate: first.OrderTruckPlate,
                Status: parsedStatus,
                Items: items
            );
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // BUILD FINAL VM
        // ───────────────────────────────────────────────────────────────────────────────
        var vm = new GetLoadingDataVm(
            DriverId: first.DriverId,
            DriverName: first.DriverName!,
            Plate: first.TruckPlate!,
            CarteSLV: request.Rfid,
            ClientName: first.ClientName,
            SapOk: first.SapOk,
            FirstWeight: first.FirstWeight,
            PabEntryAt: first.PabEntryAt,
            Order: order
        );

        return Result<GetLoadingDataVm>.Ok(vm);
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // OUTPUT RECORD MATCHES SP COLUMNS
    // ───────────────────────────────────────────────────────────────────────────────
    private sealed class LoadingRow
    {
        public int DriverId { get; init; }
        public string? DriverName { get; init; }
        public string? TruckPlate { get; init; }
        public string? ClientName { get; init; }
        public bool? SapOk { get; init; }

        public int? OrderId { get; init; }
        public string? OrderNumber { get; init; }
        public string? Destination { get; init; }
        public string? DeliveryMode { get; init; }
        public string? OrderTruckPlate { get; init; }
        public string? OrderStatus { get; init; }

        public int? ProductId { get; init; }
        public string? ProductName { get; init; }
        public decimal? Quantity { get; init; }
        public string? Unite { get; init; }
        public string? ImageUrl { get; init; }

        public decimal? FirstWeight { get; init; }
        public DateTime? PabEntryAt { get; init; }
    }
}
