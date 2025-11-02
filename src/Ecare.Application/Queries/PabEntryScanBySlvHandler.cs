// Ecare.Application/Queries/PabEntryScanBySlvHandler.cs
using Dapper;
using Ecare.Application.Dtos;
using Ecare.Application.Services;
using Ecare.Domain.Entities;
using Ecare.Domain.ValueObjects;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Queries;

public sealed class PabEntryScanBySlvHandler
    : IRequestHandler<PabEntryScanBySlvQuery, Result<ScanBySlvVm>>
{
    private readonly IDriverRepository _drivers;
    private readonly IOrderRepository _orders;
    private readonly IOrderItemRepository _orderItems;
    private readonly IEcareCimentRepository _ciments;
    private readonly IUnitOfWork _uow;
    private readonly ServiceManager _signalR;                // ⬅️ inject Azure SignalR
    private readonly ILogger<PabEntryScanBySlvHandler> _log;

    public PabEntryScanBySlvHandler(
        IDriverRepository drivers,
        IOrderRepository orders,
        IOrderItemRepository orderItems,
        IEcareCimentRepository ciments,
        IUnitOfWork uow,
        ServiceManager signalR,                               // ⬅️ inject here
        ILogger<PabEntryScanBySlvHandler> log)
    {
        _drivers = drivers;
        _orders = orders;
        _orderItems = orderItems;
        _ciments = ciments;
        _uow = uow;
        _signalR = signalR;                               // ⬅️ assign
        _log = log;
    }

    public async Task<Result<ScanBySlvVm>> Handle(PabEntryScanBySlvQuery request, CancellationToken ct)
    {
        await _uow.BeginAsync(ct);
        try
        {
            // 1) Resolve device/equipement by SLV
            var equipement = await _drivers.GetBySlvAsync(SlvId.From(request.Slv), _uow);
            if (equipement is null)
                return Result<ScanBySlvVm>.Fail("Carte SLV inconnue/inactive");

            // 2) Client lookup
            var client = await _uow.Connection.QuerySingleOrDefaultAsync<Client>(
                $@"SELECT TOP(1) * FROM {DbTableNames.Clients} WHERE RaisonSociale = @name",
                new { name = equipement.ClientName },
                _uow.Transaction);

            // 3) Current order (if any)
            var order = await _orders.GetBySlvAsync(equipement.CarteSLV, _uow);

            OrderDto? dto = null;
            if (order is not null)
            {
                var items = await _orderItems.GetByOrderIdAsync(order.Id, _uow);
                var mapped = new List<OrderItemDto>(items.Count());

                foreach (var it in items)
                {
                    // Pull product name + image from ciments
                    var product = await _ciments.GetByIdAsync(it.ProductId, _uow);
                    mapped.Add(new OrderItemDto(
                        it.ProductId,
                        product?.Name,
                        it.Quantity,
                        product?.ImageUrl,  // assumes your OrderItemDto has imageUrl in this constructor
                        it.Unite
                    ));
                }

                dto = new OrderDto(
                    order.Number,
                    order.Destination,
                    order.DeliveryMode,
                    
                    order.TruckPlate,
                    order.Status,
                    mapped);
            }

            await _uow.CommitAsync(ct);

            // 4) Build VM that your API returns
            var vm = new ScanBySlvVm(
                DriverId: equipement.Id,
                DriverName: equipement.ChauffeurName,    // NEW
                Plate: equipement.Matricule,
                CarteSLV: equipement.CarteSLV,
                ClientName: client?.Name ?? equipement.ClientName,
                SapOk: client?.SapOk,
                Order: dto
            );

            // 5) Broadcast the exact event/data expected by your frontends
            var payload = new
            {
                @event = "PabEntryDataEvent",
                site = "Asment-Temara-01",
                kiosk = "pab-entry-pc-01",
                slv = vm.CarteSLV,
                ts = DateTime.UtcNow,

                driver = new
                {
                    id = vm.DriverId,
                    plate = vm.Plate,
                    name = equipement.ChauffeurName
                },
                client = new
                {
                    name = equipement.ClientName,
                    sapOk = vm.SapOk
                },
                order = vm.Order is null ? null : new
                {
                    number = vm.Order.Number,
                    destination = vm.Order.Destination,
                    deliveryMode = vm.Order.DeliveryMode,
                    truckPlate = vm.Order.TruckPlate,
                    status = vm.Order.Status,
                    items = vm.Order.Items.Select(i => new
                    {
                        productId = i.ProductId,
                        productName = i.ProductName,
                        quantity = i.Quantity,
                        unite = i.Unite,
                        imageUrl = i.ImageUrl   // include if you added it
                    })
                }
            };

            //await SignalRHelper.BroadcastAsync(
            //    _signalR,
            //    hubName: "pabentry_data_hub",
            //    methodName: "PabEntryDataEvent",
            //    payload: payload,
            //    logger: _log,
            //    ct: ct
            //);

            _log.LogInformation("Broadcasted PabEntryDataEvent for SLV={slv}", vm.CarteSLV);

            return Result<ScanBySlvVm>.Ok(vm);
        }
        catch (Exception ex)
        {
            try { await _uow.RollbackAsync(ct); } catch { }
            _log.LogError(ex, "PabEntry scan failed for SLV={slv}", request.Slv);
            throw;
        }
    }
}
