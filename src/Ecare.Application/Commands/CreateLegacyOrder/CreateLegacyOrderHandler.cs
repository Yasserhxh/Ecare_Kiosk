using MediatR;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;

namespace Ecare.Application.Commands;
public sealed class CreateLegacyOrderHandler(
    IClientEquipementRepository equipements,
    ILegacyOrderWriter writer,
    IUnitOfWork uow)
    : IRequestHandler<CreateLegacyOrderCommand, Result<string>>
{
    public async Task<Result<string>> Handle(CreateLegacyOrderCommand request, CancellationToken ct)
    {
        if (request.Quantity <= 0) return Result<string>.Fail("Quantité invalide");

        await uow.BeginAsync(ct);
        try
        {
            var eq = await equipements.GetByCarteSlvAsync(request.Slv, uow);
            if (eq is null) return Result<string>.Fail("Carte SLV inconnue/inactive");

            var created = await writer.CreateOrderWithItemAsync(request.Slv, eq.Matricule, request.ProductId, request.Quantity, request.Unite, uow, ct);
            if (created is null) return Result<string>.Fail("Création commande échouée");

            await uow.CommitAsync(ct);
            return Result<string>.Ok(created.Value.NumeroCommande);
        }
        catch
        {
            try { await uow.RollbackAsync(ct); } catch { }
            throw;
        }
    }
}


