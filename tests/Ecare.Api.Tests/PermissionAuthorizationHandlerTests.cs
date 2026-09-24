// tests/Ecare.Api.Tests/PermissionAuthorizationHandlerTests.cs
using System.Security.Claims;
using Ecare.Api.Security;
using Microsoft.AspNetCore.Authorization;

namespace Ecare.Api.Tests;

public class PermissionAuthorizationHandlerTests
{
    private static async Task<bool> Evaluate(ClaimsPrincipal user, string permission)
    {
        var handler = new PermissionAuthorizationHandler();
        var req = new PermissionRequirement(permission);
        var ctx = new AuthorizationHandlerContext(new[] { req }, user, null);
        await handler.HandleAsync(ctx);
        return ctx.HasSucceeded;
    }

    private static ClaimsPrincipal Principal(string authMethod, string role, params string[] perms)
    {
        var claims = new List<Claim> { new("authmethod", authMethod), new(ClaimTypes.Role, role) };
        claims.AddRange(perms.Select(p => new Claim("perm", p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task Password_token_is_always_allowed()
        => Assert.True(await Evaluate(Principal("pwd", "Agent de Guichet"), "Commands.Write"));

    [Fact]
    public async Task Sso_token_allowed_when_perm_present()
        => Assert.True(await Evaluate(Principal("sso", "Agent de Guichet", "Commands.Write"), "Commands.Write"));

    [Fact]
    public async Task Sso_token_denied_when_perm_absent()
        => Assert.False(await Evaluate(Principal("sso", "Agent de Guichet", "Commands.Read"), "Commands.Write"));

    [Fact]
    public async Task Sso_token_with_no_perms_is_denied_not_thrown()
        => Assert.False(await Evaluate(Principal("sso", "Whatever"), "Commands.Write"));
}
