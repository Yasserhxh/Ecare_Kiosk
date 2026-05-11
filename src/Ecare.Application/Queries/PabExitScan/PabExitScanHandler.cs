using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ecare.Shared;

namespace Ecare.Application.Queries.PabExitScan
{
    public sealed class PabExitScanHandler
        : IRequestHandler<PabExitScanQuery, Result<PabExitScanVm>>
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<PabExitScanHandler> _log;

        public PabExitScanHandler(IConfiguration cfg, ILogger<PabExitScanHandler> log)
        {
            _cfg = cfg;
            _log = log;
        }

        public async Task<Result<PabExitScanVm>> Handle(
            PabExitScanQuery request,
            CancellationToken ct)
        {
            try
            {
                var connStr = _cfg.GetConnectionString("SqlServer");
                await using var conn = new SqlConnection(connStr);

                var row = await conn.QueryFirstOrDefaultAsync<PabExitScanVm>(
                    new CommandDefinition(
                        """
                        SELECT TOP(1)
                            L.Id AS LegendId,
                            0 AS DriverId,
                            CE.ChauffeurName AS DriverFullName,
                            L.Matricule AS Plate,
                            L.RFIDCard AS CarteSLV,
                            L.PermisDeConduite AS Cin,
                            L.ClientName,
                            L.Produit1,
                            CAST(L.Quantite1 AS DECIMAL(18,2)) AS Quantite1,
                            cim1.ImageUrl AS Produit1Image,
                            L.Produit2,
                            CAST(L.Quantite2 AS DECIMAL(18,2)) AS Quantite2,
                            cim2.ImageUrl AS Produit2Image,
                            L.PremierePoid,
                            L.DeuxiemePoid,
                            L.ParkingAt,
                            L.PabEntryAt,
                            L.StartChargingAt,
                            L.FinishedChargingAt,
                            L.PabExitAt,
                            L.ElapsedTimeParking,
                            L.ElapsedInPab_Charging,
                            L.ElapsedTimeInF_Exit,
                            L.TotalTimeInCercuit,
                            L.Ligne,
                            L.Step,
                            L.BonDeCommande,
                            L.BonDeLivraison
                        FROM dbo.Ecare_Order_Legend L
                        LEFT JOIN dbo.Ecare_ClientEquipements CE
                            ON CE.Matricule = L.Matricule
                        LEFT JOIN dbo.EcareCiments cim1
                            ON cim1.Name = L.Produit1
                        LEFT JOIN dbo.EcareCiments cim2
                            ON cim2.Name = L.Produit2
                        WHERE L.RFIDCard = @RfidCard
                          AND L.Step BETWEEN 2 AND 4
                          AND L.PabExitAt IS NULL
                        ORDER BY L.Id DESC;
                        """,
                        new { RfidCard = request.RfidCard },
                        cancellationToken: ct));

                if (row is null)
                {
                    _log.LogWarning("PAB EXIT: No data found for RFID={rfid}", request.RfidCard);
                    return Result<PabExitScanVm>.Fail("NO_DATA");
                }

                return Result<PabExitScanVm>.Ok(row);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "PAB EXIT: Error scanning RFID={rfid}", request.RfidCard);
                return Result<PabExitScanVm>.Fail("ERROR");
            }
        }
    }
}
