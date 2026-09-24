using System.IdentityModel.Tokens.Jwt;
using Ecare.Application.Auth.Services;
using Ecare.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace Ecare.Application.Tests;

public class JwtTokenServiceTests
{
    private static JwtTokenService Make()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "0a03992b72f93ee9397f0557643c83e0cb0eec9455882137c44fec74362b2146",
            ["Jwt:Issuer"] = "EcareApi",
            ["Jwt:Audience"] = "EcareClients",
        }).Build();
        return new JwtTokenService(cfg);
    }

    [Fact]
    public void Sso_token_carries_authmethod_and_perm_claims()
    {
        var svc = Make();
        var user = new ApplicationUser { Id = "u1", UserName = "said", Email = "said@x.com" };
        var jwt = svc.GenerateToken(user, "Agent de Guichet", "sso", new[] { "Commands.Read", "Commands.Write" });

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        Assert.Equal("sso", token.Claims.First(c => c.Type == "authmethod").Value);
        var perms = token.Claims.Where(c => c.Type == "perm").Select(c => c.Value).ToHashSet();
        Assert.Contains("Commands.Read", perms);
        Assert.Contains("Commands.Write", perms);
    }

    [Fact]
    public void Legacy_overload_defaults_to_pwd_with_no_perms()
    {
        var svc = Make();
        var user = new ApplicationUser { Id = "u1", UserName = "said", Email = "said@x.com" };
        var jwt = svc.GenerateToken(user, "Admin");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        Assert.Equal("pwd", token.Claims.First(c => c.Type == "authmethod").Value);
        Assert.Empty(token.Claims.Where(c => c.Type == "perm"));
    }
}
