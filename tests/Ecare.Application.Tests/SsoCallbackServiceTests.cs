// tests/Ecare.Application.Tests/SsoCallbackServiceTests.cs
// Uses a small fake over UserManager via its virtual methods.
using Ecare.Application.Auth.Services;
using Ecare.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq; // add Moq 4.20.70 to tests/Ecare.Application.Tests.csproj

namespace Ecare.Application.Tests;

public class SsoCallbackServiceTests
{
    private static UserManager<ApplicationUser> FakeUserManager(ApplicationUser? user, IList<string> roles)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mgr.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        mgr.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(roles);
        return mgr.Object;
    }

    private static JwtTokenService Jwt()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "0a03992b72f93ee9397f0557643c83e0cb0eec9455882137c44fec74362b2146",
            ["Jwt:Issuer"] = "EcareApi", ["Jwt:Audience"] = "EcareClients",
        }).Build();
        return new JwtTokenService(cfg);
    }

    [Fact]
    public async Task Missing_user_returns_null()
    {
        var svc = new SsoCallbackService(FakeUserManager(null, new List<string>()), Jwt());
        Assert.Null(await svc.BuildAuthResponseAsync("ghost@x.com"));
    }

    [Fact]
    public async Task Inactive_user_returns_null()
    {
        var u = new ApplicationUser { Id = "u1", Email = "said@x.com", UserName = "said", IsActive = false };
        var svc = new SsoCallbackService(FakeUserManager(u, new List<string> { "Agent de Guichet" }), Jwt());
        Assert.Null(await svc.BuildAuthResponseAsync("said@x.com"));
    }

    [Fact]
    public async Task User_without_profil_role_returns_null()
    {
        var u = new ApplicationUser { Id = "u1", Email = "said@x.com", UserName = "said", IsActive = true };
        var svc = new SsoCallbackService(FakeUserManager(u, new List<string> { "Client" }), Jwt());
        Assert.Null(await svc.BuildAuthResponseAsync("said@x.com"));
    }

    [Fact]
    public async Task Valid_guichet_user_gets_sso_authresponse_with_perms()
    {
        var u = new ApplicationUser { Id = "u1", Email = "said@x.com", UserName = "said", IsActive = true };
        var svc = new SsoCallbackService(FakeUserManager(u, new List<string> { "Agent de Guichet" }), Jwt());
        var res = await svc.BuildAuthResponseAsync("said@x.com");
        Assert.NotNull(res);
        Assert.Equal("sso", res!.AuthMethod);
        Assert.Equal("Agent de Guichet", res.Role);
        Assert.Contains("Commands.Write", res.Perms);
    }
}
