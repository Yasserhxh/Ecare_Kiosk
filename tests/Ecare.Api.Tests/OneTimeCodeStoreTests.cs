using Ecare.Api.Security;
using Ecare.Application.Auth.Responses;
using Microsoft.Extensions.Caching.Memory;

namespace Ecare.Api.Tests;

public class OneTimeCodeStoreTests
{
    private static AuthResponse Sample() =>
        new("tok", "said", "said@x.com", "Agent de Guichet", "u1", DateTime.UtcNow, "sso", new[] { "Commands.Read" });

    [Fact]
    public void Issued_code_can_be_consumed_once()
    {
        var store = new OneTimeCodeStore(new MemoryCache(new MemoryCacheOptions()));
        var code = store.Issue(Sample());
        Assert.True(store.TryConsume(code, out var first));
        Assert.Equal("said@x.com", first!.Email);
        Assert.False(store.TryConsume(code, out _)); // replay blocked
    }

    [Fact]
    public void Unknown_code_is_rejected()
    {
        var store = new OneTimeCodeStore(new MemoryCache(new MemoryCacheOptions()));
        Assert.False(store.TryConsume("nope", out _));
    }
}
