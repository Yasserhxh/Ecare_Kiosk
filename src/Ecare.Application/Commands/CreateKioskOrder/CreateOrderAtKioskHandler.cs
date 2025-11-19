using MediatR;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;

namespace Ecare.Application.Commands;
public sealed class CreateOrderAtKioskHandler(
    IKioskDriverRepository drivers,
    IKioskOrderRepository orders,
    IUnitOfWork uow)
    : IRequestHandler<CreateOrderAtKioskCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOrderAtKioskCommand request, CancellationToken ct)
    {
        if (request.Quantity <= 0) return Result<Guid>.Fail("Quantité invalide");
        await uow.BeginAsync(ct);
        try
        {
            var driver = await drivers.GetBySlvAsync(request.Slv, uow, ct);
            if (driver is null)
                return Result<Guid>.Fail("Carte SLV inconnue/inactive");

            var orderId = await orders.CreateAsync(driver.Value.Id, driver.Value.ClientId, request.ProductId, request.ProductType, request.ProductName, request.Unit, request.Quantity, uow, ct);
            if (orderId is null)
                return Result<Guid>.Fail("Création commande échouée");

            await uow.CommitAsync(ct);
            return Result<Guid>.Ok(orderId.Value);
        }
        catch
        {
            try { await uow.RollbackAsync(ct); } catch { }
            throw;
        }
    }
}


