using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Ecare.Application.Commands.Legend;

public sealed class UpdateSecondWeightHandler
    : IRequestHandler<UpdateSecondWeightCommand, Result<UpdateSecondWeightResult>>
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<UpdateSecondWeightHandler> _log;

    public UpdateSecondWeightHandler(IConfiguration cfg, ILogger<UpdateSecondWeightHandler> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task<Result<UpdateSecondWeightResult>> Handle(
        UpdateSecondWeightCommand request,
        CancellationToken ct)
    {
        var connStr = _cfg.GetConnectionString("SqlServer");
        if (string.IsNullOrWhiteSpace(connStr))
            return Result<UpdateSecondWeightResult>.Fail("Missing SQL connection string");

        await using var conn = new SqlConnection(connStr);

        try
        {
            var result = await conn.QueryFirstOrDefaultAsync<RowsDto>(
                "sp_UpdateSecondWeight",
                new
                {
                    RfidCard = request.RfidCard,
                    Matricule = request.Matricule,
                    DeuxiemePoid = request.DeuxiemePoid
                },
                commandType: CommandType.StoredProcedure);

            if (result is null || result.RowsAffected == 0)
                return Result<UpdateSecondWeightResult>.Fail("NO_ROW_UPDATED");

            return Result<UpdateSecondWeightResult>.Ok(new UpdateSecondWeightResult
            {
                Success = true,
                UpdatedOrderId = result.UpdatedOrderId
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error executing sp_UpdateSecondWeight for RFID={rfid}", request.RfidCard);
            return Result<UpdateSecondWeightResult>.Fail("SP_ERROR");
        }
    }

    private sealed class RowsDto
    {
        public int RowsAffected { get; set; }
        public int? UpdatedOrderId { get; set; }
    }
}
