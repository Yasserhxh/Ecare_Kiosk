using Ecare.Application.Auth.Responses;
using Microsoft.Extensions.Caching.Memory;

namespace Ecare.Api.Security;

public sealed class OneTimeCodeStore(IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public string Issue(AuthResponse response)
    {
        var code = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        cache.Set("sso-code:" + code, response, Ttl);
        return code;
    }

    public bool TryConsume(string code, out AuthResponse? response)
    {
        var key = "sso-code:" + code;
        if (cache.TryGetValue(key, out response) && response is not null)
        {
            cache.Remove(key); // single use
            return true;
        }
        response = null;
        return false;
    }
}
