using MediatR;
using Dapper;
using Ecare.Application.Dtos;
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
                ? (Id: Guid.Empty, Name: (string?)null, SapOk: (bool?)null)
                : await uow.Connection.QuerySingleOrDefaultAsync<(Guid Id, string Name, bool SapOk)>(
                    "SELECT TOP(1) Id,Name,SapOk FROM Clients WHERE Id=@id", new { id = drv.ClientId }, uow.Transaction);

            var order = await orders.GetByDriverAsync(drv.Id, uow);
            var dto = order is null ? null : new OrderDto(order.Number, order.ProductName, order.Unit, order.Quantity, order.Status);

            await uow.CommitAsync(ct);
            return Result<ScanBySlvVm>.Ok(new(drv.Id, drv.Plate, client.Name, client.SapOk, dto));
        }
        catch { await uow.RollbackAsync(ct); throw; }
    }
}
