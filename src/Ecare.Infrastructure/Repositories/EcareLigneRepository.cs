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
            string cimentName,
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
                l.Status,
                c.Id AS CimentId,
                c.Name AS CimentName,
                c.Type AS CimentType,
                c.Description AS CimentDescription
                FROM Ecare_Ligne AS l
                INNER JOIN Ecare_Zone_Chargement AS z ON l.ZoneChargementId = z.Id
                INNER JOIN Ecare_LigneCiments AS lc ON lc.LigneId = l.Id
                INNER JOIN EcareCiments AS c ON lc.CimentId = c.Id
                WHERE c.Name  = @CimentName;";

            var result = await uow.Connection.QueryAsync<LigneCimentVm>(
                sql,
                new { CimentName = cimentName },
                transaction: uow.Transaction
            );

            return result;
        }
    }
}
