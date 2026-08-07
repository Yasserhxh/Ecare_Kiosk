using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Commands.Legend;

/// <summary>
/// Enregistrement manuel (mobile) de la tare lue par la bascule du point de
/// chargement VRAC quand le flux automatique (scan -> start-charging) n'a pas
/// pu la persister (automate déconnecté). Write-once : la première valeur gagne.
/// </summary>
public sealed record SetLoadingPointTareCommand(int LegendId, int Tare) : IRequest<Result<bool>>;

public sealed class SetLoadingPointTareHandler
    : IRequestHandler<SetLoadingPointTareCommand, Result<bool>>
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<SetLoadingPointTareHandler> _log;

    public SetLoadingPointTareHandler(IConfiguration cfg, ILogger<SetLoadingPointTareHandler> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task<Result<bool>> Handle(SetLoadingPointTareCommand request, CancellationToken ct)
    {
        if (request.LegendId <= 0)
            return Result<bool>.Fail("INVALID_LEGEND_ID");

        if (request.Tare <= 0)
            return Result<bool>.Fail("INVALID_TARE");

        var connStr = _cfg.GetConnectionString("SqlServer");

        await using var conn = new SqlConnection(connStr);

        var rows = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                UPDATE dbo.Ecare_Order_Legend
                SET LoadingPointTare = @Tare
                WHERE Id = @LegendId
                  AND LoadingPointTare IS NULL;

                SELECT @@ROWCOUNT;
                """,
                new { request.LegendId, request.Tare },
                cancellationToken: ct));

        if (rows == 0)
        {
            _log.LogWarning(
                "LoadingPointTare not saved for LegendId={LegendId}: row missing or tare already set",
                request.LegendId);
            return Result<bool>.Fail("NO_ROW_UPDATED");
        }

        return Result<bool>.Ok(true);
    }
}
