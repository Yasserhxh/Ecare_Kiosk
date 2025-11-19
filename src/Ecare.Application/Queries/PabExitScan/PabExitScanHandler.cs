using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ecare.Shared;
using System.Data;

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
                using var conn = new SqlConnection(connStr);

                var row = await conn.QueryFirstOrDefaultAsync<PabExitScanVm>(
                    "sp_GetPabExitScanData",
                    new { RfidCard = request.RfidCard },
                    commandType: CommandType.StoredProcedure);

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
