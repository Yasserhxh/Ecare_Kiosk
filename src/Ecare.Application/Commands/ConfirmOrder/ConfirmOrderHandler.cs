using MediatR;
using Ecare.Infrastructure.Repositories;
using Ecare.Domain;
using Ecare.Shared;

namespace Ecare.Application.Commands;
public sealed class ConfirmOrderHandler(IOrderRepository repo, IUnitOfWork uow)
    : IRequestHandler<ConfirmOrderCommand, Result>
{
    public async Task<Result> Handle(ConfirmOrderCommand request, CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            var order = await repo.GetByNumberAsync(request.OrderNumber, uow);
            if (order is null) return Result.Fail("Commande introuvable");
            if (order.Status != OrderStatus.Cree) return Result.Fail("Statut non éligible");
            await repo.UpdateStatusAsync(order.Id, OrderStatus.Confirmee, uow);
            await uow.CommitAsync(ct);
            return Result.Ok();
        }
        catch { await uow.RollbackAsync(ct); throw; }
    }
}
