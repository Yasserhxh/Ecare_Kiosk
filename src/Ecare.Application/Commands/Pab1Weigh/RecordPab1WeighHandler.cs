using MediatR;
using Ecare.Infrastructure.Repositories;
using Ecare.Domain.Entities;
using Ecare.Shared;

namespace Ecare.Application.Commands;
public sealed class RecordPab1WeighHandler(IOrderRepository orders, IWeighRepository weighs, IUnitOfWork uow)
    : IRequestHandler<RecordPab1WeighCommand, Result>
{
    public async Task<Result> Handle(RecordPab1WeighCommand request, CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            var order = await orders.GetByNumberAsync(request.OrderNumber, uow);
            if (order is null) return Result.Fail("Commande introuvable");
            var rec = new WeighRecord { OrderId = order.Id, Stage = 1, GrossKg = request.GrossKg, TareKg = 0, NetKg = 0, TakenAt = DateTime.UtcNow };
            await weighs.InsertAsync(rec, uow);
            await uow.CommitAsync(ct);
            return Result.Ok();
        }
        catch { await uow.RollbackAsync(ct); throw; }
    }
}
