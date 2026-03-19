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
                    Quantite2 = @NewQuantite2
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

            var affectedRows = await _uow.Connection.ExecuteAsync(
                sqlUpdate,
                new
                {
                    command.OrderLegendId,
                    command.OldQuantite1,
                    command.OldQuantite2,
                    command.NewQuantite1,
                    command.NewQuantite2
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
    private class LegendQuantitiesDbRow
    {
        public decimal? Quantite1 { get; set; }
        public decimal? Quantite2 { get; set; }
    }
}
