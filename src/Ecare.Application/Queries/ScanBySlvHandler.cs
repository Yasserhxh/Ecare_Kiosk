using MediatR;
using Dapper;
using Ecare.Application.Dtos;
using Ecare.Domain.Dtos;
using Ecare.Domain.Entities;
using Ecare.Domain.Interfaces;
using Ecare.Domain.ValueObjects;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;

namespace Ecare.Application.Queries;


public sealed class ScanBySlvHandler : IRequestHandler<ScanBySlvQuery, Result<SlvDtos.ScanBySlvVm>>
{
    private readonly IDriverRepository _drivers;
    private readonly IOrderRepository _orders;
    private readonly IKioskDriverRepository _kioskDrivers;
    private readonly IKioskOrderRepository _kioskOrders;
    private readonly IOrderItemRepository _orderItems;
    private readonly IEcareCimentRepository _ciments;
    private readonly IUnitOfWork _uow;
    private readonly ISlvScanPublisher _publisher;

    public ScanBySlvHandler(
        IDriverRepository drivers,
        IOrderRepository orders,
        IKioskDriverRepository kioskDrivers,
        IKioskOrderRepository kioskOrders,
        IOrderItemRepository orderItems,
        IEcareCimentRepository ciments, 
        ISlvScanPublisher publisher,
    IUnitOfWork uow)
    {
        _drivers = drivers;
        _orders = orders;
        _kioskDrivers = kioskDrivers;
        _kioskOrders = kioskOrders;
        _orderItems = orderItems;
        _ciments = ciments;
        _uow = uow;
        _publisher = publisher;
    }

    public async Task<Result<SlvDtos.ScanBySlvVm>> Handle(ScanBySlvQuery request, CancellationToken ct)
    {
        await _uow.BeginAsync(ct);
        try
        {
            // 1) Driver/equipment by CarteSLV
            var equipement = await _drivers.GetBySlvAsync(SlvId.From(request.Slv), _uow);
            if (equipement is null)
                return Result<SlvDtos.ScanBySlvVm>.Fail("Carte SLV inconnue/inactive");

            // 2) Client by RaisonSociale (client name taken from equipment)
            var client = await _uow.Connection.QuerySingleOrDefaultAsync<Client>(
                $@"SELECT TOP(1) *
                       FROM {DbTableNames.Clients}
                       WHERE RaisonSociale = @clientName",
                new { clientName = equipement.ClientName },
                _uow.Transaction);

            // 3) Legacy order by SLV
            var order = await _orders.GetBySlvAsync(equipement.CarteSLV, _uow);

            SlvDtos.OrderDto? orderDto = null;
            if (order is not null)
            {
                // 4) Items by OrderId
                var items = await _orderItems.GetByOrderIdAsync(order.Id, _uow);

                // 5) Enrich items with product info
                var itemDtos = new List<SlvDtos.OrderItemDto>(items.Count());
                foreach (var it in items)
                {
                    var product = await _ciments.GetByIdAsync(it.ProductId, _uow);
                    itemDtos.Add(new SlvDtos.OrderItemDto(
                        it.ProductId,
                        product?.Name,
                        it.Quantity,
                        it.Unite
                    ));
                }

                orderDto = new SlvDtos.OrderDto(
                    order.Number,
                    order.Destination,
                    order.DeliveryMode,
                    order.TruckPlate,
                    order.Status,
                    itemDtos
                );
            }

            await _uow.CommitAsync(ct);

            // Build the VM you return
            var vm = new SlvDtos.ScanBySlvVm(
                equipement.Id,          // DriverId or EquipmentId, per your model
                equipement.Matricule,   // Plate
                equipement.CarteSLV,    // SLV value
                client?.Name,           // ClientName
                client?.SapOk,          // SapOk (drop if you decided to remove it)
                orderDto
            );

            _ = Task.Run(() => _publisher.PublishAsync(vm, ct), ct);
            return Result<SlvDtos.ScanBySlvVm>.Ok(vm);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }
}
