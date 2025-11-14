using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.ToggleLigneStatus
{
    public sealed class ToggleLigneStatusHandler(
        IUnitOfWork uow,
        ILogger<ToggleLigneStatusHandler> log)
        : IRequestHandler<ToggleLigneStatusCommand, Result<int>>
    {
        public async Task<Result<int>> Handle(
            ToggleLigneStatusCommand request,
            CancellationToken ct)
        {
            try
            {
                await uow.BeginAsync(ct);

                var conn = uow.Connection
                           ?? throw new InvalidOperationException("UnitOfWork.Connection is null in ToggleLigneStatusHandler.");

                // 1) Lire le statut actuel
                const string sqlSelect = """
                SELECT Status
                FROM Ecare_Ligne
                WHERE Id = @LigneId;
                """;

                var currentStatus = await conn.ExecuteScalarAsync<int?>(
                    new CommandDefinition(
                        sqlSelect,
                        new { request.LigneId },
                        transaction: uow.Transaction,
                        cancellationToken: ct));

                if (currentStatus is null)
                {
                    await uow.RollbackAsync(ct);
                    return Result<int>.Fail("Ligne introuvable.");
                }

                // 2) Calcul du nouveau statut : 0 -> 1, 1 -> 0 (autres valeurs traitées comme 0)
                var oldStatus = currentStatus.Value;
                var newStatus = oldStatus == 1 ? 0 : 1;

                // 3) Mise à jour
                const string sqlUpdate = """
                UPDATE Ecare_Ligne
                SET Status = @NewStatus
                WHERE Id = @LigneId;
                """;

                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sqlUpdate,
                        new { request.LigneId, NewStatus = newStatus },
                        transaction: uow.Transaction,
                        cancellationToken: ct));

                await uow.CommitAsync(ct);

                return Result<int>.Ok(newStatus);
            }
            catch (Exception ex)
            {
                log.LogError(ex,
                    "Erreur lors du changement de statut de la ligne {LigneId}",
                    request.LigneId);

                await uow.RollbackAsync(ct);
                return Result<int>.Fail("Erreur technique lors du changement de statut de la ligne.");
            }
        }
    }
}
