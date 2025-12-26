using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.ToggleLigneCiment
{
    namespace Ecare.Application.Commands.Cements
    {
        public sealed class ToggleLigneCimentHandler(
       IUnitOfWork uow,
       ILogger<ToggleLigneCimentHandler> log)
       : IRequestHandler<ToggleLigneCimentCommand, Result<bool>>
        {
            public async Task<Result<bool>> Handle(
                ToggleLigneCimentCommand request,
                CancellationToken ct)
            {
                try
                {
                    await uow.BeginAsync(ct);

                    var conn = uow.Connection
                        ?? throw new InvalidOperationException("UnitOfWork.Connection is null.");

                    //
                    // STEP 1 — Check if record exists
                    //
                    const string selectSql = """
                SELECT Actif 
                FROM Ecare_LigneCiments
                WHERE LigneId = @LigneId AND CimentId = @CimentId;
                """;

                    var currentActif = await conn.QueryFirstOrDefaultAsync<int?>(
                        new CommandDefinition(
                            selectSql,
                            new { request.LigneId, request.CimentId },
                            transaction: uow.Transaction,
                            cancellationToken: ct));

                    //
                    // STEP 2 — If record NOT found → create it with Actif = 1
                    //
                    if (currentActif is null)
                    {
                        const string insertSql = """
                    INSERT INTO Ecare_LigneCiments (LigneId, CimentId, Actif)
                    VALUES (@LigneId, @CimentId, 1);
                    """;

                        await conn.ExecuteAsync(
                            new CommandDefinition(
                                insertSql,
                                new { request.LigneId, request.CimentId },
                                transaction: uow.Transaction,
                                cancellationToken: ct));

                        await uow.CommitAsync(ct);
                        return Result<bool>.Ok(true); // Newly created Actif = true
                    }

                    //
                    // STEP 3 — If record found → toggle Actif
                    //
                    int newActif = currentActif.Value == 1 ? 0 : 1;

                    const string updateSql = """
                UPDATE Ecare_LigneCiments
                SET Actif = @NewActif
                WHERE LigneId = @LigneId AND CimentId = @CimentId;
                """;

                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            updateSql,
                            new { request.LigneId, request.CimentId, NewActif = newActif },
                            transaction: uow.Transaction,
                            cancellationToken: ct));

                    await uow.CommitAsync(ct);
                    return Result<bool>.Ok(newActif == 1);
                }
                catch (Exception ex)
                {
                    log.LogError(ex,
                        "Erreur Toggle Ligne/Ciment LigneId={LigneId}, CimentId={CimentId}",
                        request.LigneId, request.CimentId);

                    await uow.RollbackAsync(ct);
                    return Result<bool>.Fail("Erreur interne.");
                }
            }
        }
    }
}
