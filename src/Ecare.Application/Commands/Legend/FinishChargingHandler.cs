using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
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
            "sp_FinishCharging",
            new
            {
                RfidCard = request.RfidCard,
                Matricule = request.Matricule,
                NumberSacs_Charged = request.NumberSacs_Charged,
                Weight_Charged = request.Weight_Charged
            },
            commandType: CommandType.StoredProcedure);

        //Move it To Exe Automate

        if (request.Weight_Charged > 0 && !string.IsNullOrWhiteSpace(request.DeviceName))
        {
            var deviceId = request.DeviceName;
            await SignalRHelper.BroadcastToDeviceAsync(
                _signalR,
                "send_finish_charging_hub",
                "SendFinishCharging",
                deviceId,
                "Finished Success",
                _log,
                ct
            );
        }

        _log.LogInformation("ParkingInbound: Sent payload for SLV={slv} -> device={deviceId}", request.RfidCard, request.DeviceName);

        if (rows == 0)
            return Result<bool>.Fail("NO_ROW_UPDATED");

        return Result<bool>.Ok(true);
    }


}
