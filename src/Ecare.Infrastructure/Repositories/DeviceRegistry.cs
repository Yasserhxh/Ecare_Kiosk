using System.Collections.Concurrent;

namespace Ecare.Infrastructure.Repositories;
public interface IDeviceRegistry {
    bool TryGetSecret(string deviceId, out byte[] secret);
    void RegisterOrUpdate(string deviceId, byte[] secret);
}
public class DeviceRegistry : IDeviceRegistry {

    private readonly ConcurrentDictionary<string, byte[]> _secrets = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetSecret(string deviceId, out byte[] secret)
        => _secrets.TryGetValue(deviceId, out secret!);

    public void RegisterOrUpdate(string deviceId, byte[] secret)
        => _secrets[deviceId] = secret;

}
