using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ecare.Shared;

namespace Ecare.Application.Queries.LegendSnapshot;

public sealed class LegendSnapshotHandler
    : IRequestHandler<LegendSnapshotQuery, Result<LegendSnapshotVm>>
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<LegendSnapshotHandler> _log;

    public LegendSnapshotHandler(IConfiguration cfg, ILogger<LegendSnapshotHandler> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task<Result<LegendSnapshotVm>> Handle(
        LegendSnapshotQuery request,
        CancellationToken ct)
    {
        try
        {
            var connStr = _cfg.GetConnectionString("SqlServer");
            if (string.IsNullOrWhiteSpace(connStr))
                return Result<LegendSnapshotVm>.Fail("NO_CONNECTION_STRING");

            await using var conn = new SqlConnection(connStr);

            var row = await conn.QueryFirstOrDefaultAsync<LegendSnapshotVm>(
                new CommandDefinition(
                    """
                    SELECT TOP 1
                        lg.Id AS LegendId,
                        lg.ClientName,
                        lg.Chantier,
                        lg.Matricule,
                        lg.RFIDCard,
                        lg.TypeCamion,
                        lg.TypeProduit,
                        lg.BonDeCommande,
                        lg.Ligne,
                        lg.PremierePoid,
                        lg.DeuxiemePoid,
                        lg.ParkingAt,
                        lg.PabEntryAt,
                        lg.StartChargingAt,
                        lg.FinishedChargingAt,
                        lg.PabExitAt,
                        lg.Step,
                        lg.SacNumber,
                        lg.NumberSacs_Charged,
                        lg.Weight_Charged,
                        d.Id AS DriverId,
                        d.Nom AS DriverNom,
                        d.Prenom AS DriverPrenom,
                        lg.ChauffeurName AS DriverFullName,
                        t.Id AS TruckId,
                        t.Matricule AS TruckPlate,
                        tt.Type AS TruckTypeName,
                        lg.Produit1,
                        CAST(lg.Quantite1 AS FLOAT) AS Quantite1,
                        cim1.ImageUrl AS Produit1Image,
                        lg.Produit2,
                        CAST(lg.Quantite2 AS FLOAT) AS Quantite2,
                        cim2.ImageUrl AS Produit2Image,
                        ln.Ligne_ImageUrl AS LigneImage,
                        cim1.QualityCode,
                        cim2.QualityCode AS QaualityCode2
                    FROM dbo.Ecare_Order_Legend lg
                    LEFT JOIN dbo.Ecare_Truck t
                        ON t.Matricule = lg.Matricule
                    LEFT JOIN dbo.Ecare_Driver d
                        ON d.Id = t.DriverId
                    LEFT JOIN dbo.Ecare_Truck_Type tt
                        ON tt.Id = t.TruckTypeId
                    LEFT JOIN dbo.EcareCiments cim1
                        ON cim1.Name = lg.Produit1
                    LEFT JOIN dbo.EcareCiments cim2
                        ON cim2.Name = lg.Produit2
                    LEFT JOIN dbo.Ecare_Ligne ln
                        ON ln.Nom = lg.Ligne
                    WHERE CAST(lg.RFIDCard AS NVARCHAR(50)) = @RfidCard
                    ORDER BY lg.Id DESC;
                    """,
                    new { RfidCard = request.RfidCard },
                    cancellationToken: ct));

            if (row is null)
            {
                _log.LogWarning("LegendSnapshot: No data for RFID={rfid}", request.RfidCard);
                return Result<LegendSnapshotVm>.Fail("NO_DATA");
            }

            return Result<LegendSnapshotVm>.Ok(row);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "LegendSnapshot: Error for RFID={rfid}", request.RfidCard);
            return Result<LegendSnapshotVm>.Fail("SP_ERROR");
        }
    }
}
