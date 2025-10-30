using MediatR;
using Dapper;
using Ecare.Application.Dtos;
using Ecare.Domain.Entities;
using Ecare.Domain.ValueObjects;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Queries;

public sealed class GetPabExitDataHandler : IRequestHandler<GetPabExitDataQuery, Result<ScanBySlvPabExit>>
{
    private readonly IDriverRepository _drivers;
    private readonly IOrderRepository _orders;
    private readonly IKioskDriverRepository _kioskDrivers;
    private readonly IKioskOrderRepository _kioskOrders;
    private readonly IOrderItemRepository _orderItems;
    private readonly IEcareCimentRepository _ciments;
    private readonly IUnitOfWork _uow;
    private readonly ServiceManager _signalR;
    private readonly ILogger<GetPabExitDataHandler> _log;

    public GetPabExitDataHandler(
        IDriverRepository drivers,
        IOrderRepository orders,
        IKioskDriverRepository kioskDrivers,
        IKioskOrderRepository kioskOrders,
        IOrderItemRepository orderItems,
        IEcareCimentRepository ciments,
        IUnitOfWork uow,
        ServiceManager signalR,
        ILogger<GetPabExitDataHandler> log)
    {
        _drivers = drivers;
        _orders = orders;
        _kioskDrivers = kioskDrivers;
        _kioskOrders = kioskOrders;
        _orderItems = orderItems;
        _ciments = ciments;
        _uow = uow;
        _signalR = signalR;
        _log = log;
    }

    public async Task<Result<ScanBySlvPabExit>> Handle(GetPabExitDataQuery request, CancellationToken ct)
    {
        await _uow.BeginAsync(ct);
        try
        {
            // 1️⃣ Lookup driver from Ecare_ClientEquipements
            var equipement = await _drivers.GetBySlvAsync(SlvId.From(request.Slv), _uow);
            if (equipement is null)
                return Result<ScanBySlvPabExit>.Fail("Carte SLV inconnue/inactive");

            // 2️⃣ Lookup client info
            var client = await _uow.Connection.QuerySingleOrDefaultAsync<Client>(
                $@"SELECT TOP(1) * FROM {DbTableNames.Clients} WHERE RaisonSociale = @clientName",
                new { clientName = equipement?.ClientName },
                _uow.Transaction);

           

            // 3️⃣ Lookup current order
            var order = await _orders.GetBySlvAsync(equipement.CarteSLV, _uow);

            var firstWeight = await _uow.Connection.QuerySingleOrDefaultAsync<int>(
               $@"SELECT FirstWeight FROM {DbTableNames.Flux} WHERE BonDeCommande = @bonDeCommande",
               new { bonDeCommande = order?.NumeroCommande },
               _uow.Transaction);

            OrderDto? dto = null;

            if (order is not null)
            {
                // 4️⃣ Get order items
                var orderItemsList = await _orderItems.GetByOrderIdAsync(order.Id, _uow);

                // 5️⃣ Optimize: Fetch all product images in a single query
                var productIds = orderItemsList.Select(i => i.ProductId).Distinct().ToArray();
                var images = await GetImageUrlsAsync(productIds, _uow, ct);

                // 6️⃣ Map order items
                var orderItemsWithProducts = new List<OrderItemDto>();
                foreach (var item in orderItemsList)
                {
                    var product = await _ciments.GetByIdAsync(item.ProductId, _uow);
                    var imageUrl = images.TryGetValue(item.ProductId, out var url) ? url : null;

                    orderItemsWithProducts.Add(new OrderItemDto(
                        item.ProductId,
                        product?.Name,
                        item.Quantity,
                        item.Unite,
                        imageUrl
                    ));
                }

                dto = new OrderDto(
                    order.Number,
                    order.Destination,
                    order.DeliveryMode,
                    order.TruckPlate,
                    order.Status,
                    orderItemsWithProducts);
            }

            await _uow.CommitAsync(ct);

            var result = new ScanBySlvPabExit(
                firstWeight,
                equipement.Id,
                equipement.Matricule,
                equipement.CarteSLV,
                equipement.ChauffeurName,
                client?.SapOk,
                dto);

            // 7️⃣ Build SignalR payload
            var payload = new
            {
                @event = "ExitDataEvent",
                site = "Asment-Temara-01",
                kiosk = "pab-exit-pc-01",
                slv = result.CarteSLV,
                ts = DateTime.UtcNow,
                firstWeight = firstWeight,
                driver = new
                {
                    id = result.DriverId,
                    name = equipement.ChauffeurName,
                    plate = equipement.Matricule
                },
                client = new
                {
                    name = equipement.ClientName,
                    sapOk = client?.SapOk
                },
                order = result.Order is null ? null : new
                {
                    number = result.Order.Number,
                    destination = result.Order.Destination,
                    deliveryMode = result.Order.DeliveryMode,
                    truckPlate = result.Order.TruckPlate,
                    status = result.Order.Status,
                    items = result.Order.Items.Select(i => new
                    {
                        productId = i.ProductId,
                        productName = i.ProductName,
                        quantity = i.Quantity,
                        unite = i.Unite,
                        imageUrl = i.ImageUrl
                    })
                }
            };

            // 8️⃣ Broadcast via Azure SignalR
            await SignalRHelper.BroadcastAsync(
                _signalR,
                hubName: "pab_exit_data_hub",
                methodName: "ExitDataEvent",
                payload: payload,
                logger: _log,
                ct: ct
            );

            _log.LogInformation("Broadcasted OrderDataEvent for SLV={slv}", result.CarteSLV);

            return Result<ScanBySlvPabExit>.Ok(result);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error in ScanBySlvHandler for SLV={slv}", request.Slv);
            try { await _uow.RollbackAsync(ct); } catch { }
            throw;
        }
    }

    // 🔹 Helper: Batch get all image URLs for product IDs
    private async Task<Dictionary<int, string?>> GetImageUrlsAsync(IEnumerable<int> productIds, IUnitOfWork uow, CancellationToken ct)
    {
        if (!productIds.Any())
            return new();

        const string sql = @"SELECT Id, ImageUrl FROM [dbo].[EcareCiments] WHERE Id IN @ids";
        var cmd = new CommandDefinition(sql, new { ids = productIds }, transaction: uow.Transaction, cancellationToken: ct);

        var results = await uow.Connection.QueryAsync<(int Id, string? ImageUrl)>(cmd);
        return results.ToDictionary(x => x.Id, x => x.ImageUrl);
    }
}
