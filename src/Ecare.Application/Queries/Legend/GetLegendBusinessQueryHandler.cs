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
    ParkingAt                     AS DateCreation,
    Matricule,
    RFIDCard                      AS RFID,
    TypeProduit                   AS Circuit,
    'C'                            AS TypeOperation,
    CodeSapChantier               AS CodeChantier,
    Chantier                      AS NomChantier,
    CodeSapProduit1               AS CodeArticle,
    Produit1                      AS Article,

    PremierePoid                  AS Tare,
    DeuxiemePoid                  AS Gross,
    (DeuxiemePoid - PremierePoid) AS Net,
    PTAC,

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
        sql.Append("  AND Step BETWEEN 1 AND @MaxStep ");
        p.Add("@MaxStep", request.MaxStep);

        // ------------------------------------------------------------
        // DATE FILTER (default = today)
        // ------------------------------------------------------------
        var from = request.DateFrom ?? DateTime.Today;
        var to = request.DateTo ?? DateTime.Today.AddDays(1).AddTicks(-1);

        sql.Append(" AND ParkingAt BETWEEN @From AND @To");
        p.Add("@From", from);
        p.Add("@To", to);

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

        var rawData = await _uow.Connection.QueryAsync<dynamic>(
            sql.ToString(),
            p,
            _uow.Transaction
        );

        var data = await _uow.Connection.QueryAsync<LegendBusinessRow>(
            sql.ToString(),
            p,
            _uow.Transaction
        );

        await _uow.CommitAsync(ct);

        return data.ToList();
    }
}
