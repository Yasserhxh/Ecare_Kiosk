using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.ChangeLigneCapacity
{
    public sealed class ChangeLigneCapacityHandler(
        IUnitOfWork uow,
        ILogger<ChangeLigneCapacityHandler> log)
        : IRequestHandler<ChangeLigneCapacityCommand, Result<int>>
    {
        public async Task<Result<int>> Handle(
            ChangeLigneCapacityCommand request,
            CancellationToken ct)
        {
            try
            {
                if (request.Capacity < 0)
                {
                    return Result<int>.Fail("La capacité ne peut pas être négative.");
                }

                await uow.BeginAsync(ct);

                var conn = uow.Connection
                           ?? throw new InvalidOperationException("UnitOfWork.Connection is null in ChangeLigneCapacityHandler.");

                // Vérifier que la ligne existe
                const string sqlCheck = """
                SELECT COUNT(1)
                FROM Ecare_Ligne
                WHERE Id = @LigneId;
                """;

                var exists = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        sqlCheck,
                        new { request.LigneId },
                        transaction: uow.Transaction,
                        cancellationToken: ct));

                if (exists == 0)
                {
                    await uow.RollbackAsync(ct);
                    return Result<int>.Fail("Ligne introuvable.");
                }

                // Mise à jour de la capacité
                const string sqlUpdate = """
                UPDATE Ecare_Ligne
                SET Capacity = @Capacity
                WHERE Id = @LigneId;
                """;

                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sqlUpdate,
                        new { request.LigneId, request.Capacity },
                        transaction: uow.Transaction,
                        cancellationToken: ct));

                await uow.CommitAsync(ct);

                return Result<int>.Ok(request.Capacity);
            }
            catch (Exception ex)
            {
                log.LogError(ex,
                    "Erreur lors du changement de capacité de la ligne {LigneId}",
                    request.LigneId);

                await uow.RollbackAsync(ct);
                return Result<int>.Fail("Erreur technique lors du changement de capacité de la ligne.");
            }
        }
    }
}
