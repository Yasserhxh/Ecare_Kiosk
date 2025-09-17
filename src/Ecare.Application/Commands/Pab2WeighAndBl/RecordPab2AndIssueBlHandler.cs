using MediatR;
using Dapper;
using Ecare.Infrastructure.Repositories;
using Ecare.Domain;
using Ecare.Domain.Entities;
using Ecare.Infrastructure.Printing;
using Ecare.Shared;

namespace Ecare.Application.Commands;
public sealed class RecordPab2AndIssueBlHandler(IOrderRepository orders, IWeighRepository weighs, IBlPrinter printer, IUnitOfWork uow)
    : IRequestHandler<RecordPab2AndIssueBlCommand, Result<string>>
{
    public async Task<Result<string>> Handle(RecordPab2AndIssueBlCommand request, CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            var order = await orders.GetByNumberAsync(request.OrderNumber, uow);
            if (order is null) return Result<string>.Fail("Commande introuvable");

            int net = Math.Abs(request.GrossKg - request.TareKg);
            var targetKg = order.TargetQuantityTons.HasValue
                ? (int)(order.TargetQuantityTons.Value * 1000)
                : (int?)null;
            if (targetKg is > 0)
            {
                var deltaPct = Math.Abs(net - targetKg.Value) * 100m / targetKg.Value;
                if (deltaPct > request.TolerancePct)
                    return Result<string>.Fail($"Écart poids {deltaPct:F2}% > tolérance");
            }

            await weighs.InsertAsync(new WeighRecord
            {
                OrderId = order.Id, Stage = 2, GrossKg = request.GrossKg, TareKg = request.TareKg, NetKg = net, TakenAt = DateTime.UtcNow
            }, uow);

            var blNumber = $"BL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            await uow.Connection.ExecuteAsync($@"INSERT INTO {DbTableNames.BonLivraison} (Id,OrderId,BlNumber,NetKg,IssuedAt)
                VALUES (@Id,@OrderId,@BlNumber,@NetKg,SYSUTCDATETIME())",
                new { Id = Guid.NewGuid(), OrderId = order.Id, BlNumber = blNumber, NetKg = net }, uow.Transaction);

            await orders.UpdateStatusAsync(order.Id, OrderStatus.Livre, uow);

            var productLabel = order.Destination ?? order.DeliveryMode ?? "-";
            var html = $"<h1>Bon de Livraison</h1><p>Commande {order.Number}</p><p>Produit: {productLabel}</p><p>Net: {net} kg</p>";
            await printer.PrintTwoCopiesAsync(blNumber, html, ct);

            await uow.CommitAsync(ct);
            return Result<string>.Ok(blNumber);
        }
        catch { await uow.RollbackAsync(ct); throw; }
    }
}
