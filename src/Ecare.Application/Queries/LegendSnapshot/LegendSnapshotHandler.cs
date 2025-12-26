using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ecare.Shared;
using System.Data;

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
                "sp_GetFullLegendSnapshot",
                new { RfidCard = request.RfidCard },
                commandType: CommandType.StoredProcedure);

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
