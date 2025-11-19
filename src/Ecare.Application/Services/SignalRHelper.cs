using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

public static class SignalRHelper
{
    private static readonly ConcurrentDictionary<string, ServiceHubContext> _hubCache = new();

    /// <summary>
    /// Broadcasts to ALL clients (existing behavior)
    /// </summary>
    public static async Task BroadcastAsync(
        ServiceManager manager,
        string hubName,
        string methodName,
        object payload,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        var hubCtx = await GetOrCreateHubContext(manager, hubName, logger, ct);
        await hubCtx.Clients.All.SendAsync(methodName, payload, ct);
        logger?.LogInformation("Broadcasted to ALL in hub '{hub}' via '{method}'", hubName, methodName);
    }

    /// <summary>
    /// Broadcasts to SPECIFIC device group only
    /// </summary>
    public static async Task BroadcastToDeviceAsync(
        ServiceManager manager,
        string hubName,
        string methodName,
        string deviceId,
        object payload,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        var hubCtx = await GetOrCreateHubContext(manager, hubName, logger, ct);
        await hubCtx.Clients.User(deviceId).SendAsync(methodName, payload, ct);
        logger?.LogInformation("✅ Broadcasted to device '{device}' in hub '{hub}'", deviceId, hubName);
    }

    private static async Task<ServiceHubContext> GetOrCreateHubContext(
        ServiceManager manager,
        string hubName,
        ILogger? logger,
        CancellationToken ct)
    {
        if (!_hubCache.TryGetValue(hubName, out var hubCtx) || hubCtx == null)
        {
            hubCtx = await manager.CreateHubContextAsync(hubName, ct);
            _hubCache[hubName] = hubCtx;
            logger?.LogInformation("Created hub context for {hub}", hubName);
        }
        return hubCtx;
    }

    public static async Task DisposeAllAsync()
    {
        foreach (var kvp in _hubCache)
        {
            await kvp.Value.DisposeAsync();
        }
        _hubCache.Clear();
    }
}