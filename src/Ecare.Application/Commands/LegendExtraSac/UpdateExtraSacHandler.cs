using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Ecare.Application.Commands.LegendExtraSac;

public sealed class UpdateExtraSacHandler
    : IRequestHandler<UpdateExtraSacCommand, Result<bool>>
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<UpdateExtraSacHandler> _log;

    public UpdateExtraSacHandler(IConfiguration cfg, ILogger<UpdateExtraSacHandler> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task<Result<bool>> Handle(UpdateExtraSacCommand request, CancellationToken ct)
    {
        try
        {
            var connStr = _cfg.GetConnectionString("SqlServer");
            await using var conn = new SqlConnection(connStr);

            var rows = await conn.ExecuteScalarAsync<int>(
                "sp_UpdateExtraSac",
                new
                {
                    LegendId = request.LegendId,
                    PlusBags = request.PlusBags,
                    MinusBags = request.MinusBags
                },
                commandType: CommandType.StoredProcedure
            );

            if (rows == 0)
                return Result<bool>.Fail("NO_ROW_UPDATED");

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error updating ExtraSac for LegendId={id}", request.LegendId);
            return Result<bool>.Fail("SP_ERROR");
        }
    }
}
