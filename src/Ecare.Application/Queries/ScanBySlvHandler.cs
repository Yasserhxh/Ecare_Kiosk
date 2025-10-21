using MediatR;
using Dapper;
using Ecare.Application.Dtos;
using Ecare.Domain.Entities;
using Ecare.Domain.ValueObjects;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;

namespace Ecare.Application.Queries;
public sealed class ScanBySlvHandler(
    IDriverRepository drivers,
    IOrderRepository orders,
    IKioskDriverRepository kioskDrivers,
    IKioskOrderRepository kioskOrders,
    IOrderItemRepository orderItems,
    IEcareCimentRepository ciments,
    IUnitOfWork uow)
    : IRequestHandler<ScanBySlvQuery, Result<ScanBySlvVm>>
{
    public async Task<Result<ScanBySlvVm>> Handle(ScanBySlvQuery request, CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            // 1. Get driver info from Ecare_ClientEquipements by CarteSLV
            // Use legacy driver table since kiosk tables don't exist
            var equipement = await drivers.GetBySlvAsync(SlvId.From(request.Slv), uow);
            if (equipement is null) return Result<ScanBySlvVm>.Fail("Carte SLV inconnue/inactive");

            // 2. Get client info from Client table where ClientName == RaisonSociale
            var client = await uow.Connection.QuerySingleOrDefaultAsync<Client>(
                $@"SELECT TOP(1) *
                    FROM {DbTableNames.Clients}
                    WHERE RaisonSociale = @clientName",
                new { clientName = equipement?.ClientName },
                uow.Transaction);

            // 3. Get order from Orders table where CarteSLV == request.SLV
            // Get order from legacy Orders table by SLV
            var order = await orders.GetBySlvAsync(equipement.CarteSLV, uow);

            OrderDto? dto = null;
            if (order is not null)
            {
                // 4. Get OrderItems by OrderId
                var orderItemsList = await orderItems.GetByOrderIdAsync(order.Id, uow);

                // 5. Get product info from EcareCiments for each OrderItem
                var orderItemsWithProducts = new List<OrderItemDto>();
                foreach (var item in orderItemsList)
                {
                    var product = await ciments.GetByIdAsync(item.ProductId, uow);
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

            await uow.CommitAsync(ct);
            return Result<ScanBySlvVm>.Ok(new(
                equipement.Id,
                equipement.Matricule,
                equipement.CarteSLV,
                client?.Name,
                client?.SapOk,
                dto));
        }
        catch { await uow.RollbackAsync(ct); throw; }
    }
}
