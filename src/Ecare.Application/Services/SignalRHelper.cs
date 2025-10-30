// SignalRHelper.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading;

public static class SignalRHelper
{
    private static readonly ConcurrentDictionary<string, ServiceHubContext> _hubCache = new();
    private static readonly SemaphoreSlim _ctxLock = new(1, 1);

    public static string DeviceGroup(string deviceId) => $"device:{deviceId}";

    public static async Task SendToDeviceGroupAsync(
        ServiceManager manager,
        string hubName,
        string methodName,
        string deviceId,
        object payload,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        var ctx = await GetOrCreateHubContext(manager, hubName, logger, ct);
        await ctx.Clients.Group(DeviceGroup(deviceId)).SendAsync(methodName, payload, ct);
        logger?.LogInformation("📡 Sent to {hub}:{method} -> {group}", hubName, methodName, DeviceGroup(deviceId));
    }

    public static async Task EnsureUserInDeviceGroupAsync(
        ServiceManager manager,
        string hubName,
        string deviceId,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        var ctx = await GetOrCreateHubContext(manager, hubName, logger, ct);
        await ctx.UserGroups.AddToGroupAsync(deviceId, DeviceGroup(deviceId), ct);
        logger?.LogInformation("🔗 Ensured user {user} in {group} on {hub}", deviceId, DeviceGroup(deviceId), hubName);
    }

    private static async Task<ServiceHubContext> GetOrCreateHubContext(
        ServiceManager manager,
        string hubName,
        ILogger? logger,
        CancellationToken ct)
    {
        if (_hubCache.TryGetValue(hubName, out var existing) && existing is not null)
            return existing;

        await _ctxLock.WaitAsync(ct);
        try
        {
            if (_hubCache.TryGetValue(hubName, out existing) && existing is not null)
                return existing;

            var created = await manager.CreateHubContextAsync(hubName, ct);
            _hubCache[hubName] = created;
            logger?.LogInformation("✅ Created hub context for {hub}", hubName);
            return created;
        }
        finally { _ctxLock.Release(); }
    }

    public static async Task DisposeAllAsync()
    {
        foreach (var kv in _hubCache)
            try { await kv.Value.DisposeAsync(); } catch { }
        _hubCache.Clear();
    }
}
