using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Ecare.Application.OfflineOrders;

/// <summary>Statuts offline à afficher / synchroniser (même filtre que SapOfflineController du web-api).</summary>
public static class OfflineOrderStatuses
{
    public static readonly IReadOnlyList<string> Pending = ["EnAttente", "Erreur", "EnCours"];
}

/// <summary>Réponse du web-api relayée au front : code HTTP + corps JSON.</summary>
public sealed record OfflineSyncProxyResult(int StatusCode, string JsonBody);

public interface IOfflineSyncProxy
{
    Task<OfflineSyncProxyResult> SyncAsync(int legendId, CancellationToken ct = default);
    Task<OfflineSyncProxyResult> SyncAllAsync(CancellationToken ct = default);
}

/// <summary>
/// La synchro SAP des commandes offline vit dans mycimar-web-api (SapOfflineController).
/// Ecare-FrontUI n'appelle que l'API kiosque : celle-ci relaie la synchro côté serveur.
/// Les erreurs 4xx du web-api (messages métier) sont relayées ; les 5xx sont masquées.
/// </summary>
public sealed class OfflineSyncProxy(HttpClient http) : IOfflineSyncProxy
{
    public const string UpstreamErrorMessage = "Échec de la synchronisation côté MyCimar API.";
    public const string UnreachableMessage = "MyCimar API injoignable. Réessayez plus tard.";
    public const string TimeoutMessage = "Délai dépassé : la synchronisation continue côté serveur. Actualisez dans quelques minutes.";

    private const int BadGateway = 502;
    private const int ServiceUnavailable = 503;
    private const int GatewayTimeout = 504;

    public Task<OfflineSyncProxyResult> SyncAsync(int legendId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(legendId);
        return PostAsync($"api/SapOffline/sync/{legendId}", ct);
    }

    public Task<OfflineSyncProxyResult> SyncAllAsync(CancellationToken ct = default) =>
        PostAsync("api/SapOffline/sync-all", ct);

    private async Task<OfflineSyncProxyResult> PostAsync(string path, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsync(path, content: null, ct);
            var status = (int)response.StatusCode;

            if (status >= 500)
                return Error(BadGateway, UpstreamErrorMessage);

            var body = await response.Content.ReadAsStringAsync(ct);
            return new OfflineSyncProxyResult(status, string.IsNullOrWhiteSpace(body) ? "{}" : body);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Error(GatewayTimeout, TimeoutMessage);
        }
        catch (HttpRequestException)
        {
            return Error(ServiceUnavailable, UnreachableMessage);
        }
    }

    // Accents lisibles ; les caractères HTML sensibles restent échappés.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private static OfflineSyncProxyResult Error(int status, string message) =>
        new(status, JsonSerializer.Serialize(new { error = message }, JsonOptions));
}
