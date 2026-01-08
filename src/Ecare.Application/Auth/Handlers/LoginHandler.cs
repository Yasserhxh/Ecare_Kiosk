using Ecare.Application.Auth.Queries;
using Ecare.Application.Auth.Responses;
using Ecare.Application.Auth.Services;
using Ecare.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Ecare.Application.Auth.Handlers;

public class LoginHandler(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    JwtTokenService jwtService) : IRequestHandler<LoginQuery, AuthResponse>
{
    public async Task<AuthResponse> Handle(LoginQuery request, CancellationToken ct)
    {
        // 1️ Try find by username
        var user = await userManager.FindByNameAsync(request.Identifier);

        // 2️ If not found, try by email
        if (user == null)
            user = await userManager.FindByEmailAsync(request.Identifier);

        if (user == null)
            throw new Exception("Invalid username or email");

        // 3️ Check if active
        if (!user.IsActive)
            throw new Exception("User account is deactivated");

        // 4️ Check password
        var passwordOk = await signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!passwordOk.Succeeded)
            throw new Exception("Invalid credentials");

        // 5️ Get role(s)
        var roles = await userManager.GetRolesAsync(user);
        var userId= user.Id;
        var role = roles.FirstOrDefault() ?? "User";

        // 6️ Generate JWT
        var token = jwtService.GenerateToken(user, role);

        return new AuthResponse(
            token,
            user.UserName!,
            user.Email!,
            role,
            userId,
            DateTime.UtcNow.AddHours(2)
        );
    }
}
