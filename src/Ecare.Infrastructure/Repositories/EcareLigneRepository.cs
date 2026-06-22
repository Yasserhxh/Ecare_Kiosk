using Dapper;
using Ecare.Shared;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ecare.Infrastructure.Repositories
{
    public class EcareLigneRepository
    {
        public async Task<IEnumerable<LigneCimentVm>> GetLignesByTypeAndCimentAsync(
            int produit,
            IUnitOfWork uow,
            CancellationToken ct = default)
        {
            const string sql = @"
                SELECT
                z.Id AS ZoneId,
                z.Usine,
                z.TypeOperation,
                z.TypeActivite,
                l.Id AS LigneId,
                l.Nom AS LigneNom,
                l.Capacity,
                ISNULL(l.RealtimeCapacity, 0) AS RealtimeCapacity,
                CASE
                    WHEN (l.Capacity - occ.Occupancy) < ISNULL(l.RealtimeCapacity, l.Capacity)
                    THEN (l.Capacity - occ.Occupancy)
                    ELSE ISNULL(l.RealtimeCapacity, l.Capacity)
                END AS Available,
                l.Status,
                c.Id AS CimentId,
                c.Name AS CimentName,
                c.Type AS CimentType,
                c.Description AS CimentDescription
                FROM Ecare_Ligne AS l
                INNER JOIN Ecare_Zone_Chargement AS z ON l.ZoneChargementId = z.Id
                INNER JOIN Ecare_LigneCiments AS lc ON lc.LigneId = l.Id
                INNER JOIN EcareCiments AS c ON lc.CimentId = c.Id
                CROSS APPLY (
                    SELECT COUNT(*) AS Occupancy
                    FROM Ecare_Order_Legend O
                    WHERE O.Ligne = l.Nom
                      AND O.Step BETWEEN 2 AND 4
                      AND ISNULL(O.AnnulationCommercial, 0) <> 1
                      AND ISNULL(O.Status, '') <> 'Canceled'
                ) occ
                WHERE c.Id  = @Id;";

            var result = await uow.Connection.QueryAsync<LigneCimentVm>(
                sql,
                new { Id = produit},
                transaction: uow.Transaction
            );

            return result;
        }
    }
}
