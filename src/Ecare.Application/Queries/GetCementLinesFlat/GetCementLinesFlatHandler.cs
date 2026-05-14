using Dapper;
using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetCementLinesFlat
{
    public sealed class GetCementLinesFlatHandler(
        IUnitOfWork uow,
        ILogger<GetCementLinesFlatHandler> log)
        : IRequestHandler<GetCementLinesFlatQuery, Result<IReadOnlyList<CementLineFlatVm>>>
    {
        public async Task<Result<IReadOnlyList<CementLineFlatVm>>> Handle(
            GetCementLinesFlatQuery request,
            CancellationToken ct)
        {
            const string sql = """
            SELECT
                z.Usine AS ZoneName,
                z.TypeOperation AS OperationType,
                l.Id AS LigneId,
                l.Nom AS LineName,
                l.Status AS LineStatus,
                l.Capacity AS LineCapacity,
                ISNULL(l.RealtimeCapacity, 0) AS LineRealtimeCapacity,

                STRING_AGG(c.Name, ', ') WITHIN GROUP (ORDER BY c.Name) AS Products

            FROM Ecare_Ligne l
            JOIN Ecare_Zone_Chargement z
                ON l.ZoneChargementId = z.Id
            LEFT JOIN Ecare_LigneCiments lc
                ON lc.LigneId = l.Id
            LEFT JOIN EcareCiments c
                ON c.Id = lc.CimentId

            WHERE ('ASMENT-TEMARA' IS NULL OR UPPER(z.Usine) = UPPER('ASMENT-TEMARA'))

            GROUP BY
                z.Usine,
                z.TypeOperation,
                l.Id,
                l.Nom,
                l.Status,
                l.Capacity,
                l.RealtimeCapacity

            ORDER BY
                z.Usine,
                z.TypeOperation,
                l.Nom;
            """;

            try
            {
                await uow.BeginAsync(ct);

                var conn = uow.Connection
                           ?? throw new InvalidOperationException("UnitOfWork.Connection is null in GetCementLinesFlatHandler.");

                var rows = (await conn.QueryAsync<CementLineFlatVm>(
                    new CommandDefinition(
                        sql,
                        new { Usine = request.Usine },
                        transaction: uow.Transaction,
                        cancellationToken: ct)))
                    .ToList();

                await uow.CommitAsync(ct);

                return Result<IReadOnlyList<CementLineFlatVm>>.Ok(rows);
            }
            catch (Exception ex)
            {
                log.LogError(ex,
                    "Erreur lors du chargement des lignes/ciments pour l'usine {Usine}",
                    request.Usine);

                await uow.RollbackAsync(ct);
                return Result<IReadOnlyList<CementLineFlatVm>>.Fail(
                    "Erreur lors du chargement de la liste des lignes et produits.");
            }
        }
    }
}
