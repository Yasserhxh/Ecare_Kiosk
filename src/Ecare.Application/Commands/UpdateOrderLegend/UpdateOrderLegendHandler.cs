using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.UpdateOrderLegend
{
    public sealed class UpdateOrderLegendHandler
    : IRequestHandler<UpdateOrderLegendCommand, Result<string>>
    {
        private readonly IUnitOfWork _uow;

        public UpdateOrderLegendHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Result<string>> Handle(UpdateOrderLegendCommand request, CancellationToken ct)
        {
            const string sqlGetType = @"
            SELECT TOP 1 Type
            FROM EcareCiments
            WHERE Name = @Name;
        ";

            // Ordered bags must always follow the ordered quantity for SAC/PAL (tonnage wins).
            // @ComputedSacNumber is derived in C# from the new quantities and bag weights; when it is
            // null (not SAC/PAL, or PoidKg unknown) the existing SacNumber is preserved.
            const string sqlUpdate = @"
            UPDATE Ecare_Order_Legend
            SET
                ClientName = @ClientName,
                Chantier = @Chantier,
                Produit1 = @Produit1,
                Quantite1 = @Quantite1,
                Produit2 = @Produit2,
                Quantite2 = @Quantite2,
                TypeProduit = @TypeProduit,
                SacNumber = CASE WHEN @ComputedSacNumber IS NOT NULL THEN @ComputedSacNumber ELSE SacNumber END
            WHERE Id = @OrderId;
        ";

            const string sqlGetPoidKg = @"
            SELECT TOP 1 PoidKg
            FROM EcareCiments
            WHERE Name = @Name;
        ";

            try
            {
                await _uow.BeginAsync(ct);

                // 1) Resolve TypeProduit
                var typeProduit = await _uow.Connection.ExecuteScalarAsync<string?>(
                    sqlGetType,
                    new { Name = request.Produit1 },
                    _uow.Transaction
                );

                if (string.IsNullOrWhiteSpace(typeProduit))
                {
                    await _uow.RollbackAsync(ct);
                    return Result<string>.Fail("Produit1 not found in EcareCiments.");
                }

                // 2) Derive ordered bags from the new quantities for SAC/PAL (tonnage wins).
                int? computedSacNumber = null;
                if (IsSacOrPal(typeProduit))
                {
                    var poidKg1 = await _uow.Connection.ExecuteScalarAsync<int?>(
                        sqlGetPoidKg, new { Name = request.Produit1 }, _uow.Transaction);

                    int? poidKg2 = null;
                    if (!string.IsNullOrWhiteSpace(request.Produit2))
                        poidKg2 = await _uow.Connection.ExecuteScalarAsync<int?>(
                            sqlGetPoidKg, new { Name = request.Produit2 }, _uow.Transaction);

                    var computed =
                        ComputeSacsFromQuantity(request.Quantite1, poidKg1)
                        + ComputeSacsFromQuantity(request.Quantite2, poidKg2);

                    if (computed > 0)
                        computedSacNumber = computed;
                }

                // 3) Update Order Legend
                await _uow.Connection.ExecuteAsync(
                    sqlUpdate,
                    new
                    {
                        request.OrderId,
                        request.ClientName,
                        request.Chantier,
                        request.Produit1,
                        request.Quantite1,
                        request.Produit2,
                        request.Quantite2,
                        TypeProduit = typeProduit,
                        ComputedSacNumber = computedSacNumber
                    },
                    _uow.Transaction
                );

                await _uow.CommitAsync(ct);
                return Result<string>.Ok("Order updated successfully.");
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<string>.Fail(ex.Message);
            }
        }

        private static bool IsSacOrPal(string? typeProduit)
            => !string.IsNullOrWhiteSpace(typeProduit)
               && (typeProduit.Contains("SAC", StringComparison.OrdinalIgnoreCase)
                   || typeProduit.Contains("PAL", StringComparison.OrdinalIgnoreCase));

        // Ordered bags for one product line: ceil(quantityTons * 1000 / bagWeightKg); 0 when not derivable.
        private static int ComputeSacsFromQuantity(decimal? quantiteTonnes, int? poidKg)
        {
            if (!quantiteTonnes.HasValue || quantiteTonnes.Value <= 0 ||
                !poidKg.HasValue || poidKg.Value <= 0)
                return 0;

            return (int)Math.Ceiling((quantiteTonnes.Value * 1000m) / poidKg.Value);
        }
    }
}
