using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ecare.Shared;
using Ecare.Application.Queries.PabEntryScan;
namespace Ecare.Application.Queries.PabEntryScan
{
    public sealed class PabEntryScanHandler
        : IRequestHandler<PabEntryScanQuery, Result<PabEntryScanVm>>
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<PabEntryScanHandler> _log;

        public PabEntryScanHandler(IConfiguration cfg, ILogger<PabEntryScanHandler> log)
        {
            _cfg = cfg;
            _log = log;
        }

        public async Task<Result<PabEntryScanVm>> Handle(
            PabEntryScanQuery request,
            CancellationToken ct)
        {
            try
            {
                var connStr = _cfg.GetConnectionString("SqlServer");
                using var conn = new SqlConnection(connStr);

                const string sql = """
                    SELECT TOP 1
                        L.Id AS LegendId,
                        L.ClientName,
                        CE.PTAC,
                        CE.TARE,
                        L.Chantier,
                        L.BonDeCommande,
                        L.Matricule,
                        L.RFIDCard,
                        L.ChauffeurName AS FullName,
                        L.PremierePoid,
                        L.Produit1,
                        L.Quantite1,
                        L.Produit2,
                        L.Quantite2,
                        cim1.ImageUrl AS Image1,
                        cim2.ImageUrl AS Image2
                    FROM dbo.Ecare_Order_Legend L
                    LEFT JOIN dbo.Ecare_ClientEquipements CE
                        ON CE.Matricule = L.Matricule
                    LEFT JOIN dbo.EcareCiments cim1
                        ON cim1.Name = L.Produit1
                    LEFT JOIN dbo.EcareCiments cim2
                        ON cim2.Name = L.Produit2
                    WHERE
                        L.RFIDCard = @RfidCard
                        AND L.ParkingAt IS NOT NULL
                        AND L.Step = 1
                    ORDER BY L.ParkingAt DESC;
                """;

                var row = await conn.QueryFirstOrDefaultAsync<PabEntryScanVm>(
                    new CommandDefinition(
                        sql,
                        new { RfidCard = request.RfidCard },
                        cancellationToken: ct));

                if (row is null)
                {
                    _log.LogWarning("No PAB entry scan data found for RFID={rfid}", request.RfidCard);
                    return Result<PabEntryScanVm>.Fail("NO_DATA");
                }

                return Result<PabEntryScanVm>.Ok(row);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error in PabEntryScanHandler RFID={rfid}", request.RfidCard);
                return Result<PabEntryScanVm>.Fail("ERROR");
            }
        }
    }
}
