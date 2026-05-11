using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Ecare.Application.Commands.Legend;

public sealed class FinishChargingHandler
    : IRequestHandler<FinishChargingCommand, Result<bool>>
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<FinishChargingHandler> _log;
    private readonly ServiceManager _signalR;

    public FinishChargingHandler(IConfiguration cfg, ILogger<FinishChargingHandler> log, ServiceManager signalR)
    {
        _cfg = cfg;
        _log = log;
        _signalR = signalR;
    }

    public async Task<Result<bool>> Handle(FinishChargingCommand request, CancellationToken ct)
    {
        var connStr = _cfg.GetConnectionString("SqlServer");



        await using var conn = new SqlConnection(connStr);

        var rows = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                DECLARE @Now DATETIME =
                    CONVERT(DATETIME, SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time');

                UPDATE L
                SET
                    NumberSacs_Charged = ISNULL(L.NumberSacs_Charged, 0) + @NumberSacs_Charged,
                    Weight_Charged = ISNULL(L.Weight_Charged, 0) + @Weight_Charged,
                    Step = CASE
                        WHEN @Weight_Charged > 0 THEN 4
                        WHEN (ISNULL(L.NumberSacs_Charged, 0) + @NumberSacs_Charged) >= ISNULL(L.SacNumber, 0) THEN 4
                        WHEN @NumberSacs_Charged > 0 THEN 3
                        ELSE L.Step
                    END,
                    FinishedChargingAt = CASE
                        WHEN @Weight_Charged > 0 THEN @Now
                        WHEN (ISNULL(L.NumberSacs_Charged, 0) + @NumberSacs_Charged) >= ISNULL(L.SacNumber, 0) THEN @Now
                        ELSE L.FinishedChargingAt
                    END,
                    ElapsedCharging = CASE
                        WHEN @Weight_Charged > 0
                          OR (ISNULL(L.NumberSacs_Charged, 0) + @NumberSacs_Charged) >= ISNULL(L.SacNumber, 0)
                        THEN DATEDIFF(MINUTE, L.StartChargingAt, @Now)
                        ELSE L.ElapsedCharging
                    END,
                    LoadingStatus = CASE
                        WHEN @Weight_Charged > 0 THEN 'Completed'
                        WHEN (ISNULL(L.NumberSacs_Charged, 0) + @NumberSacs_Charged) >= ISNULL(L.SacNumber, 0) THEN 'Completed'
                        WHEN @NumberSacs_Charged > 0 THEN 'Pending'
                        ELSE L.LoadingStatus
                    END
                FROM dbo.Ecare_Order_Legend L
                WHERE
                    (
                        @LegendId IS NOT NULL
                        AND L.Id = @LegendId
                        AND L.Step < 5
                    )
                    OR
                    (
                        @LegendId IS NULL
                        AND L.RFIDCard = @RfidCard
                        AND L.Matricule = @Matricule
                        AND L.Step < 5
                    );

                SELECT @@ROWCOUNT;
                """,
                new
                {
                    request.LegendId,
                    RfidCard = request.RfidCard,
                    request.Matricule,
                    request.NumberSacs_Charged,
                    request.Weight_Charged
                },
                cancellationToken: ct));

        //Move it To Exe Automate

        if (rows == 0)
            return Result<bool>.Fail("NO_ROW_UPDATED");

        if (request.Weight_Charged > 0 && !string.IsNullOrWhiteSpace(request.DeviceName))
        {
            var deviceId = request.DeviceName;

            // ------- LOG PAYLOAD EXACTLY AS SENT -------
            var payload = new
            {
                eventName = "SendFinishCharging",
                device = deviceId,
                message = "Finished Success"
            };
            _log.LogInformation("FinishCharging Payload: {@payload}", payload);
            // --------------------------------------------

            await SignalRHelper.BroadcastToDeviceAsync(
                _signalR,
                "send_finish_charging_hub",
                "SendFinishCharging",
                deviceId,
                "Finished Success",
                _log,
                ct
            );

            _log.LogInformation("ParkingInbound: Sent payload for SLV={slv} -> device={deviceId}",
                request.RfidCard, request.DeviceName);
        }

        return Result<bool>.Ok(true);
    }


}
