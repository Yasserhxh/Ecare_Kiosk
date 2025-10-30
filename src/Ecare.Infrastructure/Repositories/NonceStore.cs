
using System.Collections.Concurrent;

namespace Ecare.Infrastructure.Repositories;
public interface INonceStore
{
    bool TryMarkSeen(string deviceId, string nonce);

}
public class NonceStore : INonceStore
{
    private readonly TimeSpan _ttl;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new();

    public NonceStore(TimeSpan ttl) => _ttl = ttl;

    public bool TryMarkSeen(string deviceId, string nonce)
    {
        var key = $"{deviceId}:{nonce}";
        var now = DateTimeOffset.UtcNow;
        Cleanup(now);
        return _seen.TryAdd(key, now);
    }

    private void Cleanup(DateTimeOffset now)
    {
        foreach (var kv in _seen)
            if (now - kv.Value > _ttl) _seen.TryRemove(kv.Key, out _);
    }
}
