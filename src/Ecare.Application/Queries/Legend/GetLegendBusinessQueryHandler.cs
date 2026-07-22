using Dapper;
using MediatR;
using System.Text;
using Ecare.Shared;

namespace Ecare.Application.Queries.Legend;

public sealed class GetLegendBusinessQueryHandler
    : IRequestHandler<GetLegendBusinessQuery, IReadOnlyList<LegendBusinessRow>>
{
    private readonly IUnitOfWork _uow;

    public GetLegendBusinessQueryHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<IReadOnlyList<LegendBusinessRow>> Handle(
        GetLegendBusinessQuery request,
        CancellationToken ct)
    {
        await _uow.BeginAsync(ct);

        var sql = new StringBuilder(@"
SELECT
    -- Mapping
    id,
    COALESCE(ParkingAt, CreatedAt) AS DateCreation,
    CreatedAt,
    DateAffectation,
    ParkingAt,
    PabEntryAt,
    StartChargingAt,
    FinishedChargingAt,
    PabExitAt,
    Matricule,
    RFIDCard                      AS RFID,
    ClientName,
    TransporteurName,
    ChauffeurName,
    TypeProduit                   AS Circuit,
    'C'                            AS TypeOperation,
    CodeSapChantier               AS CodeChantier,
    Chantier                      AS NomChantier,
    CodeSapProduit1               AS CodeArticle,
    Produit1                      AS Article,
    Produit2                      AS Article2,
    Quantite1,
    Quantite2,
    Ligne,
    TypeCamion,

    PremierePoid                  AS Tare,
    DeuxiemePoid                  AS Gross,
    (DeuxiemePoid - PremierePoid) AS Net,
    PTAC,
    TARE                          AS TAREVehicule,

    Step,
    CodeSapCommande               AS CommandeSap,
    BonDeLivraison                AS LivraisonSap,

    'WO'                           AS ScaleGross,
    'WI'                           AS ScaleTare,

    CASE
        WHEN CommercialOrderId IS NULL THEN 'CFR'
        ELSE 'EXW'
    END                           AS TypeLivraison,

    PlombNumber                   AS Seals,
    BonDeCommande                 AS BonCommandeClient,
    SacNumber,
    NumberSacs_Charged,
    Weight_Charged,
    IsLowCreditDeliveryRisk,
    DATEDIFF(MINUTE, DateAffectation, PabEntryAt) AS ElapsedAffectationToPab,
    ElapsedTimeParking,
    ElapsedInPab_Charging         AS ElapsedInPabCharging,
    ElapsedCharging,
    ElapsedTimeInF_Exit           AS ElapsedTimeInFExit,
    TotalTimeInCercuit,
    AddedToQueueAt,
    FirstPlaceAt,
    TimeElapsedInFirstPlace,
    StartExtraSac,
    EndExtraSac,
    ElapsedExtraSac,
    AnnulationCommercial,
    MotifAnnulationCommercial,
    CAST(UserIdAnnulationCommercial AS nvarchar(100)) AS UserIdAnnulationCommercial,

    CASE
        WHEN Produit2 !='' THEN 'Mixte'
        ELSE 'Simple'
    END                           AS TypeCommande
FROM dbo.Ecare_Order_Legend
WHERE 1 = 1
");

        var p = new DynamicParameters();

        // ------------------------------------------------------------
        // STEP FILTER (default)
        // ------------------------------------------------------------
        sql.Append("  AND ISNULL(Step, 0) BETWEEN 0 AND @MaxStep ");
        p.Add("@MaxStep", request.MaxStep);

        // ------------------------------------------------------------
        // DATE FILTER (default = today)
        // ------------------------------------------------------------
        var from = request.DateFrom ?? DateTime.Today;
        var to = request.DateTo ?? DateTime.Today.AddDays(1).AddTicks(-1);

        sql.Append(" AND COALESCE(ParkingAt, CreatedAt) BETWEEN @From AND @To");
        p.Add("@From", from);
        p.Add("@To", to);

        if (!string.IsNullOrWhiteSpace(request.DeliveryStatus))
        {
            var deliveryStatuses = request.DeliveryStatus
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(status => status.ToLowerInvariant())
                .Distinct()
                .ToList();

            if (deliveryStatuses.Count > 0)
            {
                var deliveryClauses = new List<string>();

                foreach (var status in deliveryStatuses)
                {
                    switch (status)
                    {
                        case "delivered":
                            deliveryClauses.Add("(ISNULL(AnnulationCommercial, 0) <> 1 AND ISNULL(BonDeLivraison, '') <> '')");
                            break;
                        case "in_progress":
                            deliveryClauses.Add("(ISNULL(AnnulationCommercial, 0) <> 1 AND ISNULL(BonDeLivraison, '') = '')");
                            break;
                        case "cancelled":
                            deliveryClauses.Add("(ISNULL(AnnulationCommercial, 0) = 1)");
                            break;
                    }
                }

                if (deliveryClauses.Count > 0)
                {
                    sql.Append(" AND (");
                    sql.Append(string.Join(" OR ", deliveryClauses));
                    sql.Append(')');
                }
            }
        }

        if (request.HasCreditOverrun.HasValue)
        {
            sql.Append(" AND ISNULL(IsLowCreditDeliveryRisk, 0) = @HasCreditOverrun");
            p.Add("@HasCreditOverrun", request.HasCreditOverrun.Value);
        }

        // ------------------------------------------------------------
        // HOUR FILTER
        // ------------------------------------------------------------
        if (request.HourFrom.HasValue)
        {
            sql.Append(" AND DATEPART(HOUR, ParkingAt) >= @HourFrom");
            p.Add("@HourFrom", request.HourFrom.Value);
        }

        if (request.HourTo.HasValue)
        {
            sql.Append(" AND DATEPART(HOUR, ParkingAt) <= @HourTo");
            p.Add("@HourTo", request.HourTo.Value);
        }

        // ------------------------------------------------------------
        // GENERIC COLUMN FILTERS
        // (must be DB column names)
        // ------------------------------------------------------------
        if (request.Filters != null)
        {
            int i = 0;
            foreach (var (column, value) in request.Filters)
            {
                var param = $"@f{i}";
                sql.Append($" AND {column} = {param}");
                p.Add(param, value);
                i++;
            }
        }

        sql.Append(" ORDER BY ParkingAt DESC");

        var data = await _uow.Connection.QueryAsync<LegendBusinessRow>(
            sql.ToString(),
            p,
            _uow.Transaction
        );

        await _uow.CommitAsync(ct);

        return data.ToList();
    }
}
