using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Ecare.Application.Services;
public static class SignalRHelper
{
    // Cache hub contexts to avoid recreating them every time
    private static readonly ConcurrentDictionary<string, ServiceHubContext> _hubCache = new();

    /// <summary>
    /// Broadcasts a payload to all clients connected to the given Azure SignalR hub.
    /// </summary>
    /// <param name="manager">The ServiceManager instance (from DI).</param>
    /// <param name="hubName">Name of the target hub (ex: "orders_hub").</param>
    /// <param name="methodName">Client-side method name to invoke (ex: "ReceiveUpdate").</param>
    /// <param name="payload">Object payload to send to all connected clients.</param>
    /// <param name="logger">Optional ILogger for diagnostics.</param>
    /// <param name="ct">Optional cancellation token.</param>
    public static async Task BroadcastAsync(
        ServiceManager manager,
        string hubName,
        string methodName,
        object payload,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (manager is null) throw new ArgumentNullException(nameof(manager));
        if (string.IsNullOrWhiteSpace(hubName)) throw new ArgumentException("Hub name is required.", nameof(hubName));
        if (string.IsNullOrWhiteSpace(methodName)) throw new ArgumentException("Method name is required.", nameof(methodName));

        try
        {
            // Reuse or create hub context
            if (!_hubCache.TryGetValue(hubName, out var hubCtx) || hubCtx == null)
            {
                hubCtx = await manager.CreateHubContextAsync(hubName, ct);
                _hubCache[hubName] = hubCtx;
                logger?.LogInformation("Created new hub context for {hub}", hubName);
            }

            // Broadcast payload
            await hubCtx.Clients.All.SendAsync(methodName, payload, ct);

            logger?.LogInformation("Broadcasted to hub '{hub}' via method '{method}' → {payload}",
                hubName, methodName, JsonSerializer.Serialize(payload));
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to broadcast to hub '{hub}'", hubName);
            throw;
        }
    }

    /// <summary>
    /// Dispose all cached hub contexts safely.
    /// </summary>
    public static async Task DisposeAllAsync()
    {
        foreach (var kvp in _hubCache)
        {
            await kvp.Value.DisposeAsync();
        }
        _hubCache.Clear();
    }
}
