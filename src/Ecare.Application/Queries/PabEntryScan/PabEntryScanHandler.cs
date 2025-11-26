using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ecare.Shared;
using Ecare.Application.Queries.PabEntryScan;
using System.Data;

namespace Ecare.Application.Queries.PabEntryScan
{
    public sealed class PabEntryScanHandler
        : IRequestHandler<PabEntryScanQuery, Result<PabEntryScanVm>>
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<PabEntryScanHandler> _log;

        public PabEntryScanHandler(IConfiguration cfg, ILogger<PabEntryScanHandler> log)
        {
            _cfg = cfg;
            _log = log;
        }

        public async Task<Result<PabEntryScanVm>> Handle(
            PabEntryScanQuery request,
            CancellationToken ct)
        {
            try
            {
                var connStr = _cfg.GetConnectionString("SqlServer");
                using var conn = new SqlConnection(connStr);

                var row = await conn.QueryFirstOrDefaultAsync<PabEntryScanVm>(
                    "sp_GetPabEntryScanData",
                    new { RfidCard = request.RfidCard },
                    commandType: CommandType.StoredProcedure);

                if (row is null)
                {
                    _log.LogWarning("No PAB entry scan data found for RFID={rfid}", request.RfidCard);
                    return Result<PabEntryScanVm>.Fail("NO_DATA");
                }

                return Result<PabEntryScanVm>.Ok(row);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error in PabEntryScanHandler RFID={rfid}", request.RfidCard);
                return Result<PabEntryScanVm>.Fail("ERROR");
            }
        }
    }
}
