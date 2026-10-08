using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;

namespace Ecare.Api.Security;

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options, IConfiguration config)
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionRequirement.PolicyPrefix, StringComparison.Ordinal))
            return _fallback.GetPolicyAsync(policyName);

        var builder = new AuthorizationPolicyBuilder();
        if (config.GetValue(AuthSettings.RbacEnforcedKey, false))
        {
            var perm = policyName[PermissionRequirement.PolicyPrefix.Length..];
            builder.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(perm));
        }
        else
        {
            // Rollout phase: RBAC declared on endpoints but not enforced (legacy anonymous access).
            builder.AddRequirements(new AssertionRequirement(_ => true));
        }
        return Task.FromResult<AuthorizationPolicy?>(builder.Build());
    }
}
