using MediatR;
using Ecare.Infrastructure.Repositories;
using Ecare.Domain;
using Ecare.Shared;

namespace Ecare.Application.Commands;
public sealed class StartLoadingHandler(IOrderRepository orders, IUnitOfWork uow)
    : IRequestHandler<StartLoadingCommand, Result>
{
    public async Task<Result> Handle(StartLoadingCommand request, CancellationToken ct)
    {
        if (!(request.Validator1Ok && request.Validator2Ok)) return Result.Fail("Double validation requise");
        await uow.BeginAsync(ct);
        try
        {
            var order = await orders.GetByNumberAsync(request.OrderNumber, uow);
            if (order is null) return Result.Fail("Commande introuvable");
            await orders.UpdateStatusAsync(order.Id, OrderStatus.EnTraitement, uow);
            await uow.CommitAsync(ct);
            return Result.Ok();
        }
        catch { await uow.RollbackAsync(ct); throw; }
    }
}
