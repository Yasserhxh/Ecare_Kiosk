using Dapper;
using Ecare.Application.Dtos;
using Ecare.Domain.Contracts;
using Ecare.Domain.Entities;
using Ecare.Domain.Interfaces;
using Ecare.Domain.ValueObjects;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Queries;
public sealed class ScanBySlvHandler(
    IDriverRepository drivers,
    IOrderRepository orders,
    IKioskDriverRepository kioskDrivers,
    IKioskOrderRepository kioskOrders,
    IOrderItemRepository orderItems,
    IEcareCimentRepository ciments,
    IUnitOfWork uow,
    IScanResultBroadcaster broadcaster)                // injected, but interface is Domain
    : IRequestHandler<ScanBySlvQuery, Result<ScanBySlvVm>>
{
    public async Task<Result<ScanBySlvVm>> Handle(ScanBySlvQuery request, CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            var equipement = await drivers.GetBySlvAsync(SlvId.From(request.Slv), uow);
            if (equipement is null) return Result<ScanBySlvVm>.Fail("Carte SLV inconnue/inactive");

            var client = await uow.Connection.QuerySingleOrDefaultAsync<Client>(
                $@"SELECT TOP(1) * FROM {DbTableNames.Clients}
                   WHERE RaisonSociale = @clientName",
                new { clientName = equipement?.ClientName },
                uow.Transaction);

            var order = await orders.GetBySlvAsync(equipement.CarteSLV, uow);

            OrderDto? dto = null;
            List<OrderItemSummary>? itemsSummary = null;

            if (order is not null)
            {
                var orderItemsList = await orderItems.GetByOrderIdAsync(order.Id, uow);
                var orderItemsWithProducts = new List<OrderItemDto>();
                itemsSummary = new List<OrderItemSummary>();

                foreach (var item in orderItemsList)
                {
                    var product = await ciments.GetByIdAsync(item.ProductId, uow);

                    orderItemsWithProducts.Add(new OrderItemDto(
                        item.ProductId,
                        product?.Name,
                        item.Quantity,
                        item.Unite));

                    itemsSummary.Add(new OrderItemSummary(
                        item.ProductId,
                        product?.Name,
                        item.Quantity,
                        item.Unite));
                }

                dto = new OrderDto(
                    order.Number,
                    order.Destination,
                    order.DeliveryMode,
                    order.TruckPlate,
                    order.Status,
                    orderItemsWithProducts);
            }

            var vm = new ScanBySlvVm(
                equipement.Id,
                equipement.Matricule,
                equipement.CarteSLV,
                client?.Name,
                client?.SapOk,
                dto);

            await uow.CommitAsync(ct);

            // Map to Domain contract and broadcast (Infra listens to Domain only)
            var message = new ScanResultMessage(
                DriverId: vm.DriverId,
                Plate: vm.Plate,
                CarteSLV: vm.CarteSLV,
                ClientName: vm.ClientName,
                SapOk: vm.SapOk,
                Order: order is null
                    ? null
                    : new OrderSummary(
                        Number: order.Number,
                        TruckPlate: order.TruckPlate,
                        Items: itemsSummary ?? new List<OrderItemSummary>()));

            _ = Task.Run(() => broadcaster.BroadcastAsync(message, ct)); // non-blocking

            return Result<ScanBySlvVm>.Ok(vm);
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }
    }
}
