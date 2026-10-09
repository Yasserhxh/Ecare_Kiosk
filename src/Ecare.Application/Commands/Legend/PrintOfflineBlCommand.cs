using Dapper;
using Ecare.Application.Services;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecare.Application.Commands.Legend;

/// <summary>
/// Impression manuelle d'un BL « Offline » depuis l'écran Documents.
/// Aucun appel SAP : le payload est construit localement depuis la legend
/// (BonDeLivraison = null, IdMyCimar = legend Id) et diffusé vers l'imprimante.
/// Réservé aux commandes dont le BL SAP n'a pas pu être créé à la sortie.
/// </summary>
public sealed record PrintOfflineBlCommand(int Id) : IRequest<Result<BlJson>>;

public sealed class PrintOfflineBlHandler
    : IRequestHandler<PrintOfflineBlCommand, Result<BlJson>>
{
    private readonly ILogger<PrintOfflineBlHandler> _log;
    private readonly ServiceManager _signalR;
    private readonly PrintOutboundOptions _opt;
    private readonly IConfiguration _cfg;

    public PrintOfflineBlHandler(
        ILogger<PrintOfflineBlHandler> log,
        ServiceManager signalR,
        IOptions<PrintOutboundOptions> opt,
        IConfiguration cfg)
    {
        _log = log;
        _signalR = signalR;
        _opt = opt.Value;
        _cfg = cfg;
    }

    public async Task<Result<BlJson>> Handle(PrintOfflineBlCommand request, CancellationToken ct)
    {
        if (request.Id <= 0)
            return Result<BlJson>.Fail("INVALID_LEGEND_ID");

        var connStr = _cfg.GetConnectionString("SqlServer");
        if (string.IsNullOrWhiteSpace(connStr))
            return Result<BlJson>.Fail("MISSING_SQL_CONNECTION");

        try
        {
            await using var conn = new SqlConnection(connStr);

            var legend = await conn.QuerySingleOrDefaultAsync<OfflineBlLegendRow>(
                new CommandDefinition(
                    """
                    SELECT TOP (1)
                        Id,
                        BonDeLivraison,
                        DeuxiemePoid
                    FROM dbo.Ecare_Order_Legend
                    WHERE Id = @Id;
                    """,
                    new { request.Id },
                    cancellationToken: ct));

            if (legend is null)
                return Result<BlJson>.Fail("LEGEND_NOT_FOUND");

            // Un BL SAP existe déjà : il faut imprimer le vrai BL (re-impression normale),
            // pas un BL offline sans numéro.
            if (!string.IsNullOrWhiteSpace(legend.BonDeLivraison))
                return Result<BlJson>.Fail("BL_ALREADY_EXISTS");

            if (!legend.DeuxiemePoid.HasValue || legend.DeuxiemePoid.Value <= 0)
                return Result<BlJson>.Fail("SECOND_WEIGHT_REQUIRED_FOR_BL");

            var snap = await conn.QuerySingleOrDefaultAsync<OfflineLegendSnapshot>(
                new CommandDefinition(
                    """
                    SELECT TOP (1)
                        ClientName,
                        Chantier,
                        BonDeCommande,
                        CodeSapClient,
                        Matricule,
                        ChauffeurName,
                        TransporteurName,
                        PermisDeConduite,
                        Plombs,
                        TypeProduit,
                        CodeSapProduit1,
                        CodeSapProduit2,
                        Produit1,
                        Produit2,
                        Quantite1,
                        Quantite2,
                        SacNumber,
                        PremierePoid,
                        DeuxiemePoid,
                        PabEntryAt,
                        PabExitAt,
                        NumberSacs_Charged,
                        Weight_Charged
                    FROM dbo.Ecare_Order_Legend
                    WHERE Id = @LegendId
                    """,
                    new { LegendId = request.Id },
                    cancellationToken: ct));

            var payload = UpdateSecondWeightHandler.BuildProvisionalBlPayload(request.Id, snap);

            await SignalRHelper.BroadcastAsync(
                _signalR,
                _opt.Hub,
                _opt.Method,
                payload,
                _log,
                ct);

            _log.LogInformation("Offline BL printed manually for LegendId={Id}", request.Id);

            return Result<BlJson>.Ok(payload);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error printing offline BL for LegendId={Id}", request.Id);
            // Type + message (pas de stack trace) pour diagnostiquer depuis le front,
            // même convention que UpdateSecondWeightHandler.
            var root = ex.GetBaseException();
            return Result<BlJson>.Fail($"UNEXPECTED_ERROR: {root.GetType().Name}: {root.Message}");
        }
    }

    private sealed class OfflineBlLegendRow
    {
        public int Id { get; init; }
        public string? BonDeLivraison { get; init; }
        public int? DeuxiemePoid { get; init; }
    }
}
