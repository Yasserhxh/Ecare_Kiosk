using Microsoft.AspNetCore.Authorization;

namespace Ecare.Api.Security;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
    public const string PolicyPrefix = "Perm:";
}
