using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Ecare.Application.Commands.Legend;

public sealed class UpdateAfterFirstWeightHandler
    : IRequestHandler<UpdateAfterFirstWeightCommand, Result<FirstWeightResultVm>>
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<UpdateAfterFirstWeightHandler> _log;

    public UpdateAfterFirstWeightHandler(
        IConfiguration cfg,
        ILogger<UpdateAfterFirstWeightHandler> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task<Result<FirstWeightResultVm>> Handle(
        UpdateAfterFirstWeightCommand request,
        CancellationToken ct)
    {
        var connStr = _cfg.GetConnectionString("SqlServer");
        if (string.IsNullOrWhiteSpace(connStr))
            return Result<FirstWeightResultVm>.Fail("Missing SQL connection string");

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
            // ⭐ stored procedure returns exactly ONE ROW:
            // LigneId, LigneName, LigneImageUrl
            var row = await conn.QueryFirstOrDefaultAsync<FirstWeightResultVm>(
                "sp_UpdateAfterFirstWeight",
                p,
                commandType: CommandType.StoredProcedure
            );

            if (row is null)
                return Result<FirstWeightResultVm>.Fail("NO_MATCHING_ROW");

            return Result<FirstWeightResultVm>.Ok(row);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Error running sp_UpdateAfterFirstWeight for RFID={rfid}",
                request.RfidCard);

            return Result<FirstWeightResultVm>.Fail("SP_ERROR");
        }
    }
}
