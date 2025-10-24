using MediatR;
using Dapper;
using Ecare.Application.Dtos;
using Ecare.Domain.Entities;
using Ecare.Domain.ValueObjects;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using Ecare.Application.Services;

namespace Ecare.Application.Queries;

public sealed class ScanBySlvHandler : IRequestHandler<ScanBySlvQuery, Result<ScanBySlvVm>>
{
    private readonly IDriverRepository _drivers;
    private readonly IOrderRepository _orders;
    private readonly IKioskDriverRepository _kioskDrivers;
    private readonly IKioskOrderRepository _kioskOrders;
    private readonly IOrderItemRepository _orderItems;
    private readonly IEcareCimentRepository _ciments;
    private readonly IUnitOfWork _uow;
    private readonly ServiceManager _signalR;
    private readonly ILogger<ScanBySlvHandler> _log;

    public ScanBySlvHandler(
        IDriverRepository drivers,
        IOrderRepository orders,
        IKioskDriverRepository kioskDrivers,
        IKioskOrderRepository kioskOrders,
        IOrderItemRepository orderItems,
        IEcareCimentRepository ciments,
        IUnitOfWork uow,
        ServiceManager signalR,
        ILogger<ScanBySlvHandler> log)
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

    public async Task<Result<ScanBySlvVm>> Handle(ScanBySlvQuery request, CancellationToken ct)
    {
        await _uow.BeginAsync(ct);
        try
        {
            //Lookup driver from Ecare_ClientEquipements (CarteSLV)
            var equipement = await _drivers.GetBySlvAsync(SlvId.From(request.Slv), _uow);
            if (equipement is null)
                return Result<ScanBySlvVm>.Fail("Carte SLV inconnue/inactive");

            //Lookup client info
            var client = await _uow.Connection.QuerySingleOrDefaultAsync<Client>(
                $@"SELECT TOP(1) * FROM {DbTableNames.Clients} WHERE RaisonSociale = @clientName",
                new { clientName = equipement?.ClientName },
                _uow.Transaction);

            //Lookup current order
            var order = await _orders.GetBySlvAsync(equipement.CarteSLV, _uow);

            OrderDto? dto = null;
            if (order is not null)
            {
                //Get order items + product names
                var orderItemsList = await _orderItems.GetByOrderIdAsync(order.Id, _uow);
                var orderItemsWithProducts = new List<OrderItemDto>();

                foreach (var item in orderItemsList)
                {
                    var product = await _ciments.GetByIdAsync(item.ProductId, _uow);
                    orderItemsWithProducts.Add(new OrderItemDto(
                        item.ProductId,
                        product?.Name,
                        item.Quantity,
                        item.Unite
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

            var result = new ScanBySlvVm(
                equipement.Id,
                equipement.Matricule,
                equipement.CarteSLV,
                client?.Name,
                client?.SapOk,
                dto);

            //Broadcast result to Azure SignalR (order_data_hub)
            var payload = new
            {
                @event = "OrderDataEvent",
                site = "Asment-Temara-01",
                kiosk = "parking-pc-01",
                slv = result.CarteSLV,
                ts = DateTime.UtcNow,
                driver = new
                {
                    id = result.DriverId,
                    name = equipement.ChauffeurName,
                    plate = equipement.Matricule
                },
                client = new
                {
                    //name = client?.Name,
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
                        unite = i.Unite
                    })
                }
            };

                await SignalRHelper.BroadcastAsync(
                _signalR,
                hubName: "order_data_hub",
                methodName: "OrderDataEvent",
                payload: payload,
                logger: _log,
                ct: ct
                );

            _log.LogInformation("Broadcasted OrderDataEvent for SLV={slv}", result.CarteSLV);

            return Result<ScanBySlvVm>.Ok(result);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error in ScanBySlvHandler for SLV={slv}", request.Slv);
            try { await _uow.RollbackAsync(ct); } catch { }
            throw;
        }
    }
}
