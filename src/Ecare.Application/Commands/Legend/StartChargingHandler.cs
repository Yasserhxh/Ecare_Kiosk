using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Commands.Legend;

public sealed class StartChargingHandler
    : IRequestHandler<StartChargingCommand, Result<bool>>
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<StartChargingHandler> _log;

    public StartChargingHandler(IConfiguration cfg, ILogger<StartChargingHandler> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task<Result<bool>> Handle(StartChargingCommand request, CancellationToken ct)
    {
        var connStr = _cfg.GetConnectionString("SqlServer");

        await using var conn = new SqlConnection(connStr);

        var rows = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                DECLARE @Now DATETIME =
                    CONVERT(DATETIME, SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time');

                UPDATE dbo.Ecare_Order_Legend
                SET
                    StartChargingAt = @Now,
                    Step = 3,
                    ElapsedInPab_Charging = DATEDIFF(MINUTE, PabEntryAt, @Now),
                    -- Tare bascule de ligne (comparaison avec PremierePoid) : write-once,
                    -- valeurs non positives ignorées.
                    LoadingPointTare = COALESCE(LoadingPointTare,
                        CASE WHEN @LoadingPointTare > 0 THEN @LoadingPointTare END)
                WHERE
                    (
                        @LegendId IS NOT NULL
                        AND Id = @LegendId
                        AND Step = 2
                    )
                    OR
                    (
                        @LegendId IS NULL
                        AND RFIDCard = @RfidCard
                        AND Matricule = @Matricule
                        AND Step = 2
                    );

                SELECT @@ROWCOUNT;
                """,
                new
                {
                    request.LegendId,
                    RfidCard = request.RfidCard,
                    request.Matricule,
                    request.LoadingPointTare
                },
                cancellationToken: ct));

        if (rows == 0)
            return Result<bool>.Fail("NO_ROW_UPDATED");

        return Result<bool>.Ok(true);
    }
}
