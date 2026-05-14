using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Commands.ChangeLigneRealtimeCapacity
{
    public sealed class ChangeLigneRealtimeCapacityHandler(
        IUnitOfWork uow,
        ILogger<ChangeLigneRealtimeCapacityHandler> log)
        : IRequestHandler<ChangeLigneRealtimeCapacityCommand, Result<int>>
    {
        public async Task<Result<int>> Handle(
            ChangeLigneRealtimeCapacityCommand request,
            CancellationToken ct)
        {
            try
            {
                if (request.RealtimeCapacity < 0)
                {
                    return Result<int>.Fail("La capacité temps réel ne peut pas être négative.");
                }

                await uow.BeginAsync(ct);

                var conn = uow.Connection
                    ?? throw new InvalidOperationException("UnitOfWork.Connection is null in ChangeLigneRealtimeCapacityHandler.");

                const string sqlGetLine = """
                SELECT Capacity
                FROM Ecare_Ligne
                WHERE Id = @LigneId;
                """;

                var lineCapacity = await conn.ExecuteScalarAsync<int?>(
                    new CommandDefinition(
                        sqlGetLine,
                        new { request.LigneId },
                        transaction: uow.Transaction,
                        cancellationToken: ct));

                if (lineCapacity is null)
                {
                    await uow.RollbackAsync(ct);
                    return Result<int>.Fail("Ligne introuvable.");
                }

                if (request.RealtimeCapacity > lineCapacity.Value)
                {
                    await uow.RollbackAsync(ct);
                    return Result<int>.Fail("La capacité temps réel ne peut pas dépasser la capacité nominale.");
                }

                const string sqlUpdate = """
                UPDATE Ecare_Ligne
                SET RealtimeCapacity = @RealtimeCapacity
                WHERE Id = @LigneId;
                """;

                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sqlUpdate,
                        new { request.LigneId, request.RealtimeCapacity },
                        transaction: uow.Transaction,
                        cancellationToken: ct));

                await uow.CommitAsync(ct);

                return Result<int>.Ok(request.RealtimeCapacity);
            }
            catch (Exception ex)
            {
                log.LogError(ex,
                    "Erreur lors du changement de capacité temps réel de la ligne {LigneId}",
                    request.LigneId);

                await uow.RollbackAsync(ct);
                return Result<int>.Fail("Erreur technique lors du changement de la capacité temps réel de la ligne.");
            }
        }
    }
}
