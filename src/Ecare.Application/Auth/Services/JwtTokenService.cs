using Ecare.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Ecare.Application.Auth.Services;

public class JwtTokenService(IConfiguration config)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(365);

    public string GenerateToken(ApplicationUser user, string role)
        => GenerateToken(user, role, "pwd", Array.Empty<string>());

    public string GenerateToken(ApplicationUser user, string role, string authMethod, IEnumerable<string> perms)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? ""),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(ClaimTypes.Role, role),
            new("authmethod", authMethod),
        };
        claims.AddRange(perms.Select(p => new Claim("perm", p)));

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(TokenLifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public DateTime GetExpirationUtc() => DateTime.UtcNow.Add(TokenLifetime);
}
