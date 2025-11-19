using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;

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
            "sp_StartCharging",
            new
            {
                RfidCard = request.RfidCard,
                Matricule = request.Matricule
            },
            commandType: CommandType.StoredProcedure);

        if (rows == 0)
            return Result<bool>.Fail("NO_ROW_UPDATED");

        return Result<bool>.Ok(true);
    }
}
