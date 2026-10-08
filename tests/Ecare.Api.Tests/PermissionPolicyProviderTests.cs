using System.Security.Claims;
using Ecare.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Ecare.Api.Tests;

public class PermissionPolicyProviderTests
{
    private static PermissionPolicyProvider Provider(bool? rbacEnforced)
    {
        var values = new Dictionary<string, string?>();
        if (rbacEnforced is not null) values[AuthSettings.RbacEnforcedKey] = rbacEnforced.ToString();
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()), cfg);
    }

    private static async Task<bool> Allowed(PermissionPolicyProvider provider, ClaimsPrincipal user)
    {
        var policy = (await provider.GetPolicyAsync("Perm:Commands.Write"))!;
        var ctx = new AuthorizationHandlerContext(policy.Requirements, user, null);
        foreach (var req in policy.Requirements.OfType<AssertionRequirement>())
            await req.HandleAsync(ctx);
        return !policy.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().Any()
               && !policy.Requirements.OfType<PermissionRequirement>().Any()
               && ctx.HasSucceeded;
    }

    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    [Fact]
    public async Task RbacEnforced_missing_defaults_to_open_access_for_anonymous()
        => Assert.True(await Allowed(Provider(null), Anonymous));

    [Fact]
    public async Task RbacEnforced_false_lets_anonymous_through_perm_policies()
        => Assert.True(await Allowed(Provider(false), Anonymous));

    [Fact]
    public async Task RbacEnforced_true_requires_authenticated_user_and_permission()
    {
        var policy = (await Provider(true).GetPolicyAsync("Perm:Commands.Write"))!;
        Assert.Contains(policy.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
        Assert.Contains(policy.Requirements, r => r is PermissionRequirement { Permission: "Commands.Write" });
    }

    [Fact]
    public async Task Non_perm_policy_names_fall_back_to_default_provider()
        => Assert.Null(await Provider(false).GetPolicyAsync("SomethingElse"));
}
