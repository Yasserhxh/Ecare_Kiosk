using Microsoft.AspNetCore.Authorization;

namespace Ecare.Api.Security;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var authMethod = context.User.FindFirst("authmethod")?.Value;

        // Password (legacy) sessions are exempt this delivery.
        if (authMethod == "pwd")
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // SSO sessions: enforce the permission claim.
        if (authMethod == "sso" &&
            context.User.FindAll("perm").Any(c => c.Value == requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask; // otherwise: not succeeded → denied
    }
}
