using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Ecare.Application.Commands.Legend;

public sealed class UpdateAfterFirstWeightHandler
    : IRequestHandler<UpdateAfterFirstWeightCommand, Result<bool>>
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<UpdateAfterFirstWeightHandler> _log;

    public UpdateAfterFirstWeightHandler(IConfiguration cfg, ILogger<UpdateAfterFirstWeightHandler> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task<Result<bool>> Handle(UpdateAfterFirstWeightCommand request, CancellationToken ct)
    {
        var connStr = _cfg.GetConnectionString("SqlServer");
        if (string.IsNullOrWhiteSpace(connStr))
            return Result<bool>.Fail("Missing SQL connection string");

        await using var conn = new SqlConnection(connStr);

        var p = new
        {
            RfidCard = request.RfidCard,
            Matricule = request.Matricule,
            PremierePoid = request.PremierePoid,
            Produit1 = request.Produit1
        };

        try
        {
            var rows = await conn.ExecuteAsync(
                "sp_UpdateAfterFirstWeight",
                p,
                commandType: CommandType.StoredProcedure);

            if (rows == 0)
                return Result<bool>.Fail("NO_MATCHING_ROW");

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error while running sp_UpdateAfterFirstWeight for RFID {rfid}", request.RfidCard);
            return Result<bool>.Fail("SP_ERROR");
        }
    }
}
