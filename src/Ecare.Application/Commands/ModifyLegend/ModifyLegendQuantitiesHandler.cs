using Dapper;
using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.ModifyLegend;

public class ModifyLegendQuantitiesHandler : IRequestHandler<ModifyLegendQuantitiesCommand, ModifyLegendQuantitiesResult>
{
    private readonly IUnitOfWork _uow;

    public ModifyLegendQuantitiesHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ModifyLegendQuantitiesResult> Handle(ModifyLegendQuantitiesCommand command, CancellationToken cancellationToken)
    {
        const string sqlExists = @"
                SELECT COUNT(1)
                FROM Ecare_Order_Legend
                WHERE Id = @OrderLegendId;";

        const string sqlUpdate = @"
                UPDATE Ecare_Order_Legend
                SET
                    Quantite1 = @NewQuantite1,
                    Quantite2 = @NewQuantite2,
                    SacNumber = CASE WHEN @NewSacNumber IS NOT NULL THEN @NewSacNumber ELSE SacNumber END
                WHERE Id = @OrderLegendId
                  AND (
                        (Quantite1 = @OldQuantite1)
                        OR (Quantite1 IS NULL AND @OldQuantite1 IS NULL)
                      )
                  AND (
                        (Quantite2 = @OldQuantite2)
                        OR (Quantite2 IS NULL AND @OldQuantite2 IS NULL)
                      );";

        await _uow.BeginAsync(cancellationToken);

        try
        {
            var exists = await _uow.Connection.ExecuteScalarAsync<int>(
                sqlExists,
                new { command.OrderLegendId },
                _uow.Transaction
            );

            if (exists == 0)
            {
                await _uow.RollbackAsync(cancellationToken);
                return new ModifyLegendQuantitiesResult
                {
                    Success = false,
                    Message = "Order legend introuvable."
                };
            }

            // Ordered bags must always follow the ordered quantity for SAC/PAL.
            // Derive SacNumber from the NEW quantities and each product's bag weight (PoidKg),
            // so a quantity change automatically updates the bag count.
            var productInfo = await _uow.Connection.QueryFirstOrDefaultAsync<LegendProductInfo>(
                @"SELECT
                      L.TypeProduit AS TypeProduit,
                      (SELECT TOP 1 PoidKg FROM EcareCiments c
                       WHERE c.CodeSAP = L.CodeSapProduit1 OR c.Name = L.Produit1) AS PoidKg1,
                      (SELECT TOP 1 PoidKg FROM EcareCiments c
                       WHERE c.CodeSAP = L.CodeSapProduit2 OR c.Name = L.Produit2) AS PoidKg2
                  FROM Ecare_Order_Legend L
                  WHERE L.Id = @OrderLegendId;",
                new { command.OrderLegendId },
                _uow.Transaction
            );

            int? effectiveSacNumber = command.NewSacNumber;
            if (IsSacOrPal(productInfo?.TypeProduit))
            {
                var computedSacs =
                    ComputeSacsFromQuantity(command.NewQuantite1, productInfo?.PoidKg1)
                    + ComputeSacsFromQuantity(command.NewQuantite2, productInfo?.PoidKg2);

                if (computedSacs > 0)
                    effectiveSacNumber = computedSacs;
            }

            var affectedRows = await _uow.Connection.ExecuteAsync(
                sqlUpdate,
                new
                {
                    command.OrderLegendId,
                    command.OldQuantite1,
                    command.OldQuantite2,
                    command.NewQuantite1,
                    command.NewQuantite2,
                    NewSacNumber = effectiveSacNumber
                },
                _uow.Transaction
            );

            if (affectedRows == 0)
            {
                await _uow.RollbackAsync(cancellationToken);
                return new ModifyLegendQuantitiesResult
                {
                    Success = false,
                    Message = "Les anciennes quantités ne correspondent plus aux valeurs actuelles."
                };
            }

            await _uow.CommitAsync(cancellationToken);

            return new ModifyLegendQuantitiesResult
            {
                Success = true,
                Message = "Quantités modifiées avec succès."
            };
        }
        catch
        {
            await _uow.RollbackAsync(cancellationToken);
            return new ModifyLegendQuantitiesResult
            {
                Success = false,
                Message = "Une erreur est survenue lors de la modification des quantités."
            };
        }
    }
    private static bool IsSacOrPal(string? typeProduit)
        => !string.IsNullOrWhiteSpace(typeProduit)
           && (typeProduit.Contains("SAC", StringComparison.OrdinalIgnoreCase)
               || typeProduit.Contains("PAL", StringComparison.OrdinalIgnoreCase));

    // Ordered bags for one product line: ceil(quantityTons * 1000 / bagWeightKg).
    // Returns 0 when not derivable (no quantity or no bag weight) so callers can sum lines.
    private static int ComputeSacsFromQuantity(decimal? quantiteTonnes, int? poidKg)
    {
        if (!quantiteTonnes.HasValue || quantiteTonnes.Value <= 0 ||
            !poidKg.HasValue || poidKg.Value <= 0)
            return 0;

        return (int)Math.Ceiling((quantiteTonnes.Value * 1000m) / poidKg.Value);
    }

    private sealed class LegendProductInfo
    {
        public string? TypeProduit { get; set; }
        public int? PoidKg1 { get; set; }
        public int? PoidKg2 { get; set; }
    }
}
