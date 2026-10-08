namespace Ecare.Api.Security;

/// <summary>Configuration keys for the authentication / authorization rollout switches.</summary>
public static class AuthSettings
{
    /// <summary>
    /// When false (default), every "Perm:*" policy allows anonymous callers: the API behaves as
    /// before SSO. Set true once password login is retired and all users carry a profil role.
    /// </summary>
    public const string RbacEnforcedKey = "Auth:RbacEnforced";
}
