using Dapper;
using Ecare.Application.OfflineOrders;
using Ecare.Shared;

namespace Ecare.Api.Endpoints;

/// <summary>
/// Commandes créées en mode offline (Ecare_Order_Legend.IsOffline = 1) pour la page Flow « Commandes offline ».
/// Mêmes routes que SapOfflineController (web-api) : le front n'appelle que l'API kiosque,
/// la lecture se fait ici et la synchro SAP est relayée au web-api côté serveur.
/// </summary>
public static class OfflineOrderEndpoints
{
    private const string PendingFilter = """
        IsOffline = 1
        AND OfflineStatus IN @Pending
        AND AnnulationCommercial IS NULL
        """;

    private const string OperationsSql = $"""
        SELECT Id, ClientName, Chantier, Produit1, Quantite1, Matricule, Step,
               CodeSapCommande, BonDeLivraison, OfflineStatus, OfflineSyncError, OfflineCreatedAt
        FROM dbo.Ecare_Order_Legend
        WHERE {PendingFilter}
        ORDER BY Id;
        """;

    private const string CountSql = $"""
        SELECT COUNT(*) FROM dbo.Ecare_Order_Legend WHERE {PendingFilter};
        """;

    public static IEndpointRouteBuilder MapOfflineOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/SapOffline").WithTags("OfflineOrders");

        group.MapGet("/operations", async (IDbConnectionFactory factory, CancellationToken ct) =>
        {
            using var conn = factory.Create();
            var rows = await conn.QueryAsync(new CommandDefinition(
                OperationsSql, new { Pending = OfflineOrderStatuses.Pending }, cancellationToken: ct));
            return Results.Ok(rows);
        })
        .RequireAuthorization("Perm:Commands.Read");

        group.MapGet("/count", async (IDbConnectionFactory factory, CancellationToken ct) =>
        {
            using var conn = factory.Create();
            var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                CountSql, new { Pending = OfflineOrderStatuses.Pending }, cancellationToken: ct));
            return Results.Ok(new { count });
        })
        .RequireAuthorization("Perm:Commands.Read");

        group.MapPost("/sync/{legendId:int}", async (int legendId, IOfflineSyncProxy proxy, CancellationToken ct) =>
            ToResult(await proxy.SyncAsync(legendId, ct)))
        .RequireAuthorization("Perm:Commands.Write");

        group.MapPost("/sync-all", async (IOfflineSyncProxy proxy, CancellationToken ct) =>
            ToResult(await proxy.SyncAllAsync(ct)))
        .RequireAuthorization("Perm:Commands.Write");

        return app;
    }

    private static IResult ToResult(OfflineSyncProxyResult result) =>
        Results.Content(result.JsonBody, "application/json", statusCode: result.StatusCode);
}
