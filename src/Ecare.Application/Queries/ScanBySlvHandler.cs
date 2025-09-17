using MediatR;
using Dapper;
using Ecare.Application.Dtos;
using Ecare.Domain.Entities;
using Ecare.Domain.ValueObjects;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;

namespace Ecare.Application.Queries;
public sealed class ScanBySlvHandler(IDriverRepository drivers, IOrderRepository orders, IUnitOfWork uow)
    : IRequestHandler<ScanBySlvQuery, Result<ScanBySlvVm>>
{
    public async Task<Result<ScanBySlvVm>> Handle(ScanBySlvQuery request, CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            var drv = await drivers.GetBySlvAsync(SlvId.From(request.Slv), uow);
            if (drv is null) return Result<ScanBySlvVm>.Fail("Carte SLV inconnue/inactive");

            var client = drv.ClientId is null
                ? null
                : await uow.Connection.QuerySingleOrDefaultAsync<Client>(
                    $@"SELECT TOP(1)
                            Client_Id   AS Id,
                            Nom_Complet AS Name,
                            CodeClientSap AS SapCode
                        FROM {DbTableNames.Clients}
                        WHERE Client_Id=@id",
                    new { id = drv.ClientId },
                    uow.Transaction);

            var order = await orders.GetBySlvAsync(drv.Slv.Value, uow);
            var dto = order is null
                ? null
                : new OrderDto(order.Number, order.Destination, order.DeliveryMode, order.TruckPlate, order.Status);

            await uow.CommitAsync(ct);
            return Result<ScanBySlvVm>.Ok(new(drv.Id, drv.Plate, client?.Name, client?.SapOk, dto));
        }
        catch { await uow.RollbackAsync(ct); throw; }
    }
}
