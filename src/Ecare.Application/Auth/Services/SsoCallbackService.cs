using Ecare.Application.Auth.Responses;
using Ecare.Application.Security;
using Ecare.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Ecare.Application.Auth.Services;

public class SsoCallbackService(UserManager<ApplicationUser> userManager, JwtTokenService jwt)
{
    public async Task<AuthResponse?> BuildAuthResponseAsync(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive) return null;

        var roles = await userManager.GetRolesAsync(user);
        // pick the first role that carries a permission set (profil), Admin/Admin IT included
        var role = roles.FirstOrDefault(r => PermissionMatrix.For(r).Count > 0);
        if (role is null) return null;

        var perms = PermissionMatrix.For(role).ToArray();
        var token = jwt.GenerateToken(user, role, "sso", perms);
        return new AuthResponse(token, user.UserName!, user.Email!, role, user.Id,
            jwt.GetExpirationUtc(), "sso", perms);
    }
}
