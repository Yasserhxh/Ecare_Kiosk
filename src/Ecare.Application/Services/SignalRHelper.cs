using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

public static class SignalRHelper
{
    private static readonly ConcurrentDictionary<string, ServiceHubContext> _hubCache = new();

    /// <summary>
    /// Smart broadcast:
    /// - If payload contains a string property "deviceId", sends ONLY to group "device:{deviceId}".
    /// - Otherwise, broadcasts to ALL clients.
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

        var deviceId = TryExtractDeviceId(payload);
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            // Prefer group targeting; works for browser tabs joined by negotiate
            var group = $"device:{deviceId}";
            await hubCtx.Clients.Group(group).SendAsync(methodName, payload, ct);
            logger?.LogInformation("📡 Sent '{method}' to GROUP '{group}' on hub '{hub}'", methodName, group, hubName);

            // Optional: also send to SignalR "User" (uncomment if you want dual targeting)
            // await hubCtx.Clients.User(deviceId).SendAsync(methodName, payload, ct);
            // logger?.LogInformation("📡 Sent '{method}' to USER '{user}' on hub '{hub}'", methodName, deviceId, hubName);

            return;
        }

        // No deviceId → broadcast to everyone
        await hubCtx.Clients.All.SendAsync(methodName, payload, ct);
        logger?.LogInformation("📡 Broadcasted '{method}' to ALL on hub '{hub}'", methodName, hubName);
    }

    /// <summary>
    /// Explicit device (USER) send (kept for callers that pass deviceId separately).
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
        logger?.LogInformation("📡 Sent '{method}' to USER '{device}' on hub '{hub}'", methodName, deviceId, hubName);
    }

    /// <summary>
    /// Explicit device GROUP send (preferred if your negotiate adds user to group "device:{deviceId}").
    /// </summary>
    public static async Task SendToDeviceGroupAsync(
        ServiceManager manager,
        string hubName,
        string methodName,
        string deviceId,
        object payload,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        var hubCtx = await GetOrCreateHubContext(manager, hubName, logger, ct);
        var group = $"device:{deviceId}";
        await hubCtx.Clients.Group(group).SendAsync(methodName, payload, ct);
        logger?.LogInformation("📡 Sent '{method}' to GROUP '{group}' on hub '{hub}'", methodName, group, hubName);
    }

    private static async Task<ServiceHubContext> GetOrCreateHubContext(
        ServiceManager manager,
        string hubName,
        ILogger? logger,
        CancellationToken ct)
    {
        if (!_hubCache.TryGetValue(hubName, out var hubCtx) || hubCtx is null)
        {
            hubCtx = await manager.CreateHubContextAsync(hubName, ct);
            _hubCache[hubName] = hubCtx;
            logger?.LogInformation("⚙️ Created hub context for {hub}", hubName);
        }
        return hubCtx;
    }

    private static string? TryExtractDeviceId(object payload)
    {
        // JSON path
        if (payload is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            if (je.TryGetProperty("deviceId", out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }

        // POCO reflection path
        var prop = payload.GetType().GetProperty("deviceId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        var val = prop?.GetValue(payload)?.ToString();
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }

    public static async Task DisposeAllAsync()
    {
        foreach (var kv in _hubCache)
            await kv.Value.DisposeAsync();
        _hubCache.Clear();
    }
}
