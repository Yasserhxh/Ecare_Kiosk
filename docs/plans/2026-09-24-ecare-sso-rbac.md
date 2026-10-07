# Ecare SSO + RBAC (profils guichet commercial) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Azure AD SSO login (alongside existing password login) to Ecare, and enforce the guichet-commercial read/write permission matrix for SSO sessions across the Ecare_Kiosk API and Ecare-FrontUi.

**Architecture:** Server-side OpenID Connect on the Ecare_Kiosk API (Microsoft.Identity.Web, mirroring mycimar-web-client) validates the Azure AD user, matches the email against the shared `AspNetUsers`, reads roles from the shared `AspNetUserRoles`, and mints the existing Ecare JWT — delivered to the React SPA via a short-lived one-time code. Permissions are a code-defined matrix mapped to ASP.NET authorization policies; the authorization handler exempts password (`authmethod=pwd`) tokens and enforces SSO (`authmethod=sso`) tokens.

**Tech Stack:** .NET 8 (Ecare_Kiosk: minimal APIs, EF Core Identity, MediatR, xUnit), Microsoft.Identity.Web 3.x + Microsoft.AspNetCore.Authentication.OpenIdConnect 8.0, React + Vite + TS (Ecare-FrontUi), Azure SQL (shared), Azure AD/Entra.

**Spec:** `Ecare_Kiosk/docs/specs/2026-09-24-ecare-sso-rbac-design.md` (also at repo-set root `docs/superpowers/specs/…`). Read it alongside this plan.

## Global Constraints

- **Shared database, no schema changes.** Ecare_Kiosk and mycimar-web-client share `sqldb-emea-we-dssprod-dss-001`. **Never run an EF migration against prod**; roles are added by the SQL script in Task 9 only. Do not add columns to `AspNetUsers`.
- **Both login methods coexist.** Password login (`POST /auth/login`) stays enabled (`Auth:PasswordLoginEnabled` default `true`) and behaves exactly as today. SSO is additive.
- **RBAC applies to SSO sessions only** this delivery. Password tokens (`authmethod=pwd`) are exempt (full/legacy access). SSO tokens (`authmethod=sso`) are enforced.
- **Role names (exact strings):** `Agent de Guichet`, `Logistique`, `Expédition`, `Admin IT`. Both `Admin` and `Admin IT` resolve to the Admin IT permission set.
- **Kiosk-hardware carve-out.** Unattended kiosk/device flows (RFID scan, SignalR device pushes; the `/login`, `/scanner`, cardholder→pesage kiosk journey and device hubs) must NOT be gated by agent permissions.
- **Secrets in App Service settings** (double-underscore `AzureAd__ClientSecret`), never in appsettings committed to git.
- **Target framework net8.0**; test project `tests/Ecare.Application.Tests` (xUnit 2.5.3).

## Review Focus

- **Expired/replayed one-time code** (Task 6): a `code` used twice or after ~60 s must return 400 and never mint a second token — pinned in Task 6 tests.
- **SSO user absent/inactive/no-profil-role** (Task 6): callback must redirect to `{Spa}/auth/callback?error=denied`, never 500 and never issue a token — pinned in Task 6 tests.
- **Password token reaching a `Perm:*` endpoint** (Task 3): must be allowed (exempt), so enabling enforcement in Task 8 does not break existing password users — pinned in Task 3 tests.
- **SSO token with no `perms` claim / unknown role** (Task 3): must be denied (fail-closed for SSO), not throw — pinned in Task 3 tests.
- **Anonymous request (no token) to a now-protected endpoint** (Task 8): must 401, and the kiosk/device carve-out endpoints must still return today's behavior — pinned in Task 8 verification.

---

## Task 1: Roles, Permissions, and the PermissionMatrix (single source of truth)

**Files:**
- Create: `src/Ecare.Application/Security/EcareRoles.cs`
- Create: `src/Ecare.Application/Security/Permissions.cs`
- Create: `src/Ecare.Application/Security/PermissionMatrix.cs`
- Test: `tests/Ecare.Application.Tests/PermissionMatrixTests.cs`

**Interfaces:**
- Produces:
  - `EcareRoles.AgentDeGuichet/Logistique/Expedition/AdminIT/Admin` (const strings).
  - `Permissions.*` const strings (e.g. `Permissions.CommandsWrite == "Commands.Write"`), and `Permissions.All` (`IReadOnlyList<string>`).
  - `PermissionMatrix.For(string role) : IReadOnlySet<string>` (empty set for unknown role; `Admin` maps to the Admin IT set).
  - `PermissionMatrix.Roles : IReadOnlyList<string>` (the 4 profil roles).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Ecare.Application.Tests/PermissionMatrixTests.cs
using Ecare.Application.Security;

namespace Ecare.Application.Tests;

public class PermissionMatrixTests
{
    [Fact]
    public void Guichet_has_exactly_the_matrix_row()
    {
        var expected = new HashSet<string>
        {
            "Commands.Read","Commands.Write","CommandsQuantities.Read","CommandsQuantities.Write",
            "FluxStatus.Read","ForceCall.Read","LogisticsData.Read","Rfid.Read","Bagging.Read",
            "LoadingQuotas.Read","Reports.Read","Weighing.Read","LoadingSettings.Read"
        };
        Assert.Equal(expected, PermissionMatrix.For(EcareRoles.AgentDeGuichet).ToHashSet());
    }

    [Fact]
    public void Logistique_has_exactly_the_matrix_row()
    {
        var expected = new HashSet<string>
        {
            "Commands.Read","Commands.Write","CommandsQuantities.Read","CommandsQuantities.Write",
            "FluxStatus.Read","ForceCall.Read","ForceCall.Execute","LogisticsData.Read","LogisticsData.Write",
            "Rfid.Read","Rfid.Write","Bagging.Read","LoadingQuotas.Read","Reports.Read","Weighing.Read",
            "LoadingSettings.Read"
        };
        Assert.Equal(expected, PermissionMatrix.For(EcareRoles.Logistique).ToHashSet());
    }

    [Fact]
    public void Expedition_has_exactly_the_matrix_row()
    {
        var expected = new HashSet<string>
        {
            "FluxStatus.Read","ForceCall.Read","LogisticsData.Read","Rfid.Read",
            "Bagging.Read","Bagging.Write","LoadingQuotas.Read","LoadingQuotas.Write",
            "Reports.Read","Weighing.Read","LoadingSettings.Read","LoadingSettings.Write"
        };
        Assert.Equal(expected, PermissionMatrix.For(EcareRoles.Expedition).ToHashSet());
    }

    [Fact]
    public void AdminIT_has_exactly_the_matrix_row()
    {
        var expected = new HashSet<string>
        {
            "Commands.Read","CommandsQuantities.Read","FluxStatus.Read","ForceCall.Read",
            "LogisticsData.Read","Rfid.Read","Bagging.Read","Bagging.Write","LoadingQuotas.Read",
            "Reports.Read","Weighing.Read","TraceabilityLogs.Read","LoadingSettings.Read"
        };
        Assert.Equal(expected, PermissionMatrix.For(EcareRoles.AdminIT).ToHashSet());
    }

    [Fact]
    public void Admin_maps_to_AdminIT_set()
        => Assert.Equal(PermissionMatrix.For(EcareRoles.AdminIT), PermissionMatrix.For(EcareRoles.Admin));

    [Fact]
    public void Unknown_role_has_no_permissions()
        => Assert.Empty(PermissionMatrix.For("NoSuchRole"));

    [Fact]
    public void Permissions_All_covers_every_key_used_in_the_matrix()
    {
        var used = PermissionMatrix.Roles.SelectMany(r => PermissionMatrix.For(r)).ToHashSet();
        Assert.True(used.IsSubsetOf(Permissions.All.ToHashSet()));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Ecare.Application.Tests/Ecare.Application.Tests.csproj --filter PermissionMatrixTests`
Expected: FAIL — `EcareRoles`/`Permissions`/`PermissionMatrix` do not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/Ecare.Application/Security/EcareRoles.cs
namespace Ecare.Application.Security;

public static class EcareRoles
{
    public const string AgentDeGuichet = "Agent de Guichet";
    public const string Logistique     = "Logistique";
    public const string Expedition     = "Expédition";
    public const string AdminIT        = "Admin IT";
    public const string Admin          = "Admin"; // legacy; treated as Admin IT
}
```

```csharp
// src/Ecare.Application/Security/Permissions.cs
namespace Ecare.Application.Security;

public static class Permissions
{
    public const string CommandsRead            = "Commands.Read";
    public const string CommandsWrite           = "Commands.Write";
    public const string CommandsQuantitiesRead  = "CommandsQuantities.Read";
    public const string CommandsQuantitiesWrite = "CommandsQuantities.Write";
    public const string FluxStatusRead          = "FluxStatus.Read";
    public const string ForceCallRead           = "ForceCall.Read";
    public const string ForceCallExecute        = "ForceCall.Execute";
    public const string LogisticsDataRead       = "LogisticsData.Read";
    public const string LogisticsDataWrite      = "LogisticsData.Write";
    public const string RfidRead                = "Rfid.Read";
    public const string RfidWrite               = "Rfid.Write";
    public const string BaggingRead             = "Bagging.Read";
    public const string BaggingWrite            = "Bagging.Write";
    public const string LoadingQuotasRead       = "LoadingQuotas.Read";
    public const string LoadingQuotasWrite      = "LoadingQuotas.Write";
    public const string ReportsRead             = "Reports.Read";
    public const string WeighingRead            = "Weighing.Read";
    public const string TraceabilityLogsRead    = "TraceabilityLogs.Read";
    public const string LoadingSettingsRead     = "LoadingSettings.Read";
    public const string LoadingSettingsWrite    = "LoadingSettings.Write";

    public static readonly IReadOnlyList<string> All = new[]
    {
        CommandsRead, CommandsWrite, CommandsQuantitiesRead, CommandsQuantitiesWrite,
        FluxStatusRead, ForceCallRead, ForceCallExecute, LogisticsDataRead, LogisticsDataWrite,
        RfidRead, RfidWrite, BaggingRead, BaggingWrite, LoadingQuotasRead, LoadingQuotasWrite,
        ReportsRead, WeighingRead, TraceabilityLogsRead, LoadingSettingsRead, LoadingSettingsWrite
    };
}
```

```csharp
// src/Ecare.Application/Security/PermissionMatrix.cs
using P = Ecare.Application.Security.Permissions;

namespace Ecare.Application.Security;

public static class PermissionMatrix
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Map =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
    {
        [EcareRoles.AgentDeGuichet] = new HashSet<string>
        {
            P.CommandsRead, P.CommandsWrite, P.CommandsQuantitiesRead, P.CommandsQuantitiesWrite,
            P.FluxStatusRead, P.ForceCallRead, P.LogisticsDataRead, P.RfidRead, P.BaggingRead,
            P.LoadingQuotasRead, P.ReportsRead, P.WeighingRead, P.LoadingSettingsRead
        },
        [EcareRoles.Logistique] = new HashSet<string>
        {
            P.CommandsRead, P.CommandsWrite, P.CommandsQuantitiesRead, P.CommandsQuantitiesWrite,
            P.FluxStatusRead, P.ForceCallRead, P.ForceCallExecute, P.LogisticsDataRead, P.LogisticsDataWrite,
            P.RfidRead, P.RfidWrite, P.BaggingRead, P.LoadingQuotasRead, P.ReportsRead, P.WeighingRead,
            P.LoadingSettingsRead
        },
        [EcareRoles.Expedition] = new HashSet<string>
        {
            P.FluxStatusRead, P.ForceCallRead, P.LogisticsDataRead, P.RfidRead,
            P.BaggingRead, P.BaggingWrite, P.LoadingQuotasRead, P.LoadingQuotasWrite,
            P.ReportsRead, P.WeighingRead, P.LoadingSettingsRead, P.LoadingSettingsWrite
        },
        [EcareRoles.AdminIT] = new HashSet<string>
        {
            P.CommandsRead, P.CommandsQuantitiesRead, P.FluxStatusRead, P.ForceCallRead,
            P.LogisticsDataRead, P.RfidRead, P.BaggingRead, P.BaggingWrite, P.LoadingQuotasRead,
            P.ReportsRead, P.WeighingRead, P.TraceabilityLogsRead, P.LoadingSettingsRead
        },
    };

    public static readonly IReadOnlyList<string> Roles = new[]
    { EcareRoles.AgentDeGuichet, EcareRoles.Logistique, EcareRoles.Expedition, EcareRoles.AdminIT };

    private static readonly IReadOnlySet<string> Empty = new HashSet<string>();

    public static IReadOnlySet<string> For(string role)
    {
        if (role == EcareRoles.Admin) role = EcareRoles.AdminIT; // legacy equivalence
        return Map.TryGetValue(role, out var set) ? set : Empty;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Ecare.Application.Tests/Ecare.Application.Tests.csproj --filter PermissionMatrixTests`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Ecare.Application/Security tests/Ecare.Application.Tests/PermissionMatrixTests.cs
git commit -m "feat(security): add roles, permissions, and the RBAC permission matrix"
```

---

## Task 2: Extend the JWT with authmethod + perms; extend AuthResponse; tag password login

**Files:**
- Modify: `src/Ecare.Application/Auth/Services/JwtTokenService.cs`
- Modify: `src/Ecare.Application/Auth/Responses/AuthResponse.cs`
- Modify: `src/Ecare.Application/Auth/Handlers/LoginHandler.cs`
- Test: `tests/Ecare.Application.Tests/JwtTokenServiceTests.cs`

**Interfaces:**
- Consumes: `PermissionMatrix.For` (Task 1).
- Produces:
  - `JwtTokenService.GenerateToken(ApplicationUser user, string role, string authMethod, IEnumerable<string> perms)` — new overload; keeps the old `GenerateToken(user, role)` delegating with `authMethod="pwd"`, `perms=[]`.
  - Claims added: `authmethod` (value `"pwd"` or `"sso"`) and one `perm` claim per permission.
  - `AuthResponse` gains `string AuthMethod` and `IReadOnlyList<string> Perms`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Ecare.Application.Tests/JwtTokenServiceTests.cs
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Ecare.Application.Tests/Ecare.Application.Tests.csproj --filter JwtTokenServiceTests`
Expected: FAIL — new `GenerateToken` overload not defined.

- [ ] **Step 3: Write minimal implementation**

In `JwtTokenService.cs`, replace `GenerateToken(user, role)` with:

```csharp
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
```

In `AuthResponse.cs`:

```csharp
namespace Ecare.Application.Auth.Responses;

public record AuthResponse(
    string Token,
    string UserName,
    string Email,
    string Role,
    string UserId,
    DateTime ExpiresAt,
    string AuthMethod,
    IReadOnlyList<string> Perms);
```

In `LoginHandler.cs`, update the final `return` (password path → `authmethod=pwd`, no perms):

```csharp
var token = jwtService.GenerateToken(user, role); // defaults to pwd, no perms

return new AuthResponse(
    token, user.UserName!, user.Email!, role, userId,
    jwtService.GetExpirationUtc(),
    AuthMethod: "pwd",
    Perms: Array.Empty<string>());
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Ecare.Application.Tests/Ecare.Application.Tests.csproj --filter JwtTokenServiceTests`
Expected: PASS. Then run the whole suite to ensure `AuthResponse` change didn't break callers:
Run: `dotnet build src/Ecare.Api/Ecare.Api.csproj`
Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/Ecare.Application/Auth tests/Ecare.Application.Tests/JwtTokenServiceTests.cs
git commit -m "feat(auth): add authmethod + perm claims to JWT; extend AuthResponse"
```

---

## Task 3: Permission authorization requirement + handler (pwd exempt, sso enforced)

**Files:**
- Create: `src/Ecare.Api/Security/PermissionRequirement.cs`
- Create: `src/Ecare.Api/Security/PermissionAuthorizationHandler.cs`
- Create: `src/Ecare.Api/Security/PermissionPolicyProvider.cs`
- Test: `tests/Ecare.Api.Tests/PermissionAuthorizationHandlerTests.cs` (new test project — see Step 0)

**Interfaces:**
- Consumes: `authmethod` + `perm` claims (Task 2).
- Produces:
  - `PermissionRequirement(string permission) : IAuthorizationRequirement`.
  - `PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>`.
  - `PermissionPolicyProvider : IAuthorizationPolicyProvider` that materializes policies named `Perm:<key>`.

- [ ] **Step 0: Create an API-layer test project (one-time)**

The existing `Ecare.Application.Tests` references only `Ecare.Application`; the handler lives in `Ecare.Api`. Create `tests/Ecare.Api.Tests/Ecare.Api.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Ecare.Api\Ecare.Api.csproj" />
  </ItemGroup>
</Project>
```

Add it to the solution: `dotnet sln add tests/Ecare.Api.Tests/Ecare.Api.Tests.csproj` (skip if no `.sln`).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Ecare.Api.Tests/PermissionAuthorizationHandlerTests.cs
using System.Security.Claims;
using Ecare.Api.Security;
using Microsoft.AspNetCore.Authorization;

namespace Ecare.Api.Tests;

public class PermissionAuthorizationHandlerTests
{
    private static async Task<bool> Evaluate(ClaimsPrincipal user, string permission)
    {
        var handler = new PermissionAuthorizationHandler();
        var req = new PermissionRequirement(permission);
        var ctx = new AuthorizationHandlerContext(new[] { req }, user, null);
        await handler.HandleAsync(ctx);
        return ctx.HasSucceeded;
    }

    private static ClaimsPrincipal Principal(string authMethod, string role, params string[] perms)
    {
        var claims = new List<Claim> { new("authmethod", authMethod), new(ClaimTypes.Role, role) };
        claims.AddRange(perms.Select(p => new Claim("perm", p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task Password_token_is_always_allowed()
        => Assert.True(await Evaluate(Principal("pwd", "Agent de Guichet"), "Commands.Write"));

    [Fact]
    public async Task Sso_token_allowed_when_perm_present()
        => Assert.True(await Evaluate(Principal("sso", "Agent de Guichet", "Commands.Write"), "Commands.Write"));

    [Fact]
    public async Task Sso_token_denied_when_perm_absent()
        => Assert.False(await Evaluate(Principal("sso", "Agent de Guichet", "Commands.Read"), "Commands.Write"));

    [Fact]
    public async Task Sso_token_with_no_perms_is_denied_not_thrown()
        => Assert.False(await Evaluate(Principal("sso", "Whatever"), "Commands.Write"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Ecare.Api.Tests/Ecare.Api.Tests.csproj`
Expected: FAIL — `PermissionRequirement`/`PermissionAuthorizationHandler` not defined.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/Ecare.Api/Security/PermissionRequirement.cs
using Microsoft.AspNetCore.Authorization;

namespace Ecare.Api.Security;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
    public const string PolicyPrefix = "Perm:";
}
```

```csharp
// src/Ecare.Api/Security/PermissionAuthorizationHandler.cs
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
```

```csharp
// src/Ecare.Api/Security/PermissionPolicyProvider.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Ecare.Api.Security;

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PermissionRequirement.PolicyPrefix, StringComparison.Ordinal))
        {
            var perm = policyName[PermissionRequirement.PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(perm))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return _fallback.GetPolicyAsync(policyName);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Ecare.Api.Tests/Ecare.Api.Tests.csproj`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Ecare.Api/Security tests/Ecare.Api.Tests
git commit -m "feat(security): permission requirement, handler (pwd exempt), and dynamic policy provider"
```

---

## Task 4: Register authorization + config binding in Program.cs

**Files:**
- Modify: `src/Ecare.Api/Program.cs` (the `AddAuthorization()` line and config section)
- Modify: `src/Ecare.Api/appsettings.json` (add `AzureAd`, `Spa`, `Auth` sections with empty/dev-safe values)

**Interfaces:**
- Consumes: `PermissionPolicyProvider`, `PermissionAuthorizationHandler` (Task 3).
- Produces: DI registrations so any `.RequireAuthorization("Perm:<key>")` resolves.

- [ ] **Step 1: Replace `builder.Services.AddAuthorization();`**

```csharp
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, Ecare.Api.Security.PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, Ecare.Api.Security.PermissionAuthorizationHandler>();
builder.Services.AddMemoryCache(); // used by the SSO one-time-code store (Task 6)
```

- [ ] **Step 2: Add config sections to `appsettings.json`** (secret stays blank here; set in App Service):

```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com",
  "TenantID": "",
  "ClientID": "",
  "ClientSecret": "",
  "CallbackUrl": "/signin-oidc",
  "SigoutcallbackUrl": "/signout-oidc"
},
"Spa": { "BaseUrl": "http://localhost:5173" },
"Auth": { "PasswordLoginEnabled": true }
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Ecare.Api/Ecare.Api.csproj`
Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/Ecare.Api/Program.cs src/Ecare.Api/appsettings.json
git commit -m "chore(api): register permission authorization + SSO/auth config sections"
```

---

## Task 5: Add OpenID Connect (cookie) scheme alongside JWT

**Files:**
- Modify: `src/Ecare.Api/Ecare.Api.csproj` (packages)
- Modify: `src/Ecare.Api/Program.cs` (auth wiring)

**Interfaces:**
- Produces: a second auth scheme (cookie + OIDC) named `"OpenIdConnect"` used only for the login dance; JWT stays the default for API calls.

- [ ] **Step 1: Add packages**

```bash
dotnet add src/Ecare.Api/Ecare.Api.csproj package Microsoft.Identity.Web --version 3.0.1
dotnet add src/Ecare.Api/Ecare.Api.csproj package Microsoft.Identity.Web.UI --version 3.0.1
dotnet add src/Ecare.Api/Ecare.Api.csproj package Microsoft.AspNetCore.Authentication.OpenIdConnect --version 8.0.0
```

- [ ] **Step 2: Extend the auth builder in Program.cs**

Keep JWT as default; chain the OIDC app onto the same `AddAuthentication(...)` call (mirrors mycimar). Replace the existing `.AddJwtBearer(...)` block's *closing* by appending:

```csharp
// after .AddJwtBearer(...) { ... }
    .AddMicrosoftIdentityWebApp(options =>
    {
        var azureAd = cfg.GetSection("AzureAd");
        options.Instance = azureAd["Instance"]!;
        options.TenantId = azureAd["TenantID"];
        options.ClientId = azureAd["ClientID"];
        options.ClientSecret = azureAd["ClientSecret"];
        options.CallbackPath = azureAd["CallbackUrl"];               // /signin-oidc
        options.SignedOutCallbackPath = azureAd["SigoutcallbackUrl"]; // /signout-oidc
        options.SignInScheme = Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme;
    }, cookieScheme: "EcareSsoCookie",
       openIdConnectScheme: "OpenIdConnect",
       displayName: null);
```

Guard the whole OIDC block so a missing `AzureAd:ClientID` does not crash local boot (mirrors the OverstayAlert conditional pattern already in this file): only append `.AddMicrosoftIdentityWebApp(...)` when `!string.IsNullOrWhiteSpace(cfg["AzureAd:ClientID"])`. When absent, log `⚠ SSO non configuré (AzureAd:ClientID manquant)`.

- [ ] **Step 3: Verify build + local boot**

Run: `dotnet build src/Ecare.Api/Ecare.Api.csproj`
Expected: build succeeds. Boot locally (dev, no AzureAd config) and confirm it prints the `SSO non configuré` warning and starts — password login must still work.

- [ ] **Step 4: Commit**

```bash
git add src/Ecare.Api/Ecare.Api.csproj src/Ecare.Api/Program.cs
git commit -m "feat(auth): add Azure AD OpenID Connect scheme alongside JWT (guarded)"
```

---

## Task 6: SSO endpoints + one-time-code store + callback service

**Files:**
- Create: `src/Ecare.Application/Auth/Services/SsoCallbackService.cs`
- Create: `src/Ecare.Api/Security/OneTimeCodeStore.cs`
- Create: `src/Ecare.Api/Endpoints/SsoEndpoints.cs`
- Modify: `src/Ecare.Api/Program.cs` (`app.MapSsoEndpoints()` + register `SsoCallbackService`, `OneTimeCodeStore`)
- Test: `tests/Ecare.Api.Tests/OneTimeCodeStoreTests.cs`
- Test: `tests/Ecare.Application.Tests/SsoCallbackServiceTests.cs`

**Interfaces:**
- Consumes: `UserManager<ApplicationUser>`, `JwtTokenService` (Task 2), `PermissionMatrix` (Task 1), `IMemoryCache`.
- Produces:
  - `OneTimeCodeStore.Issue(AuthResponse) : string` (returns code); `TryConsume(string code, out AuthResponse) : bool` (single-use, ~60 s TTL).
  - `SsoCallbackService.BuildAuthResponseAsync(string email) : Task<AuthResponse?>` (null → denied: user missing/inactive/no profil role).
  - Endpoints: `GET /auth/sso/login`, callback handled via OIDC middleware + `GET /auth/sso/complete`, `POST /auth/sso/exchange`, `GET /auth/sso/logout`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Ecare.Api.Tests/OneTimeCodeStoreTests.cs
using Ecare.Api.Security;
using Ecare.Application.Auth.Responses;
using Microsoft.Extensions.Caching.Memory;

namespace Ecare.Api.Tests;

public class OneTimeCodeStoreTests
{
    private static AuthResponse Sample() =>
        new("tok", "said", "said@x.com", "Agent de Guichet", "u1", DateTime.UtcNow, "sso", new[] { "Commands.Read" });

    [Fact]
    public void Issued_code_can_be_consumed_once()
    {
        var store = new OneTimeCodeStore(new MemoryCache(new MemoryCacheOptions()));
        var code = store.Issue(Sample());
        Assert.True(store.TryConsume(code, out var first));
        Assert.Equal("said@x.com", first!.Email);
        Assert.False(store.TryConsume(code, out _)); // replay blocked
    }

    [Fact]
    public void Unknown_code_is_rejected()
    {
        var store = new OneTimeCodeStore(new MemoryCache(new MemoryCacheOptions()));
        Assert.False(store.TryConsume("nope", out _));
    }
}
```

```csharp
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
```

Add Moq to the Application test project: `dotnet add tests/Ecare.Application.Tests/Ecare.Application.Tests.csproj package Moq --version 4.20.70`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Ecare.Api.Tests --filter OneTimeCodeStoreTests` and `dotnet test tests/Ecare.Application.Tests --filter SsoCallbackServiceTests`
Expected: FAIL — types not defined.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/Ecare.Application/Auth/Services/SsoCallbackService.cs
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
```

> **Deferred (ville/activité claims):** the spec's Section 5 step 6 lists `VilleId`/`activite` claims,
> but Ecare's `ApplicationUser` is a **subset** of mycimar's and does **not** expose `Id_Ville`. Reading
> them would require a separate query against the shared `AspNetUsers`/`AspNetRoles_Activités` tables.
> Per the spec's YAGNI decision (Ecare is Temara/Ciment-centric today), these claims are **scaffolded
> but not populated** in this delivery — add them here when Ecare serves multiple sites. This is a
> known, intentional gap, not an omission.

```csharp
// src/Ecare.Api/Security/OneTimeCodeStore.cs
using Ecare.Application.Auth.Responses;
using Microsoft.Extensions.Caching.Memory;

namespace Ecare.Api.Security;

public sealed class OneTimeCodeStore(IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public string Issue(AuthResponse response)
    {
        var code = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        cache.Set("sso-code:" + code, response, Ttl);
        return code;
    }

    public bool TryConsume(string code, out AuthResponse? response)
    {
        var key = "sso-code:" + code;
        if (cache.TryGetValue(key, out response) && response is not null)
        {
            cache.Remove(key); // single use
            return true;
        }
        response = null;
        return false;
    }
}
```

```csharp
// src/Ecare.Api/Endpoints/SsoEndpoints.cs
using System.Security.Claims;
using Ecare.Api.Security;
using Ecare.Application.Auth.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace Ecare.Api.Endpoints;

public static class SsoEndpoints
{
    public static IEndpointRouteBuilder MapSsoEndpoints(this IEndpointRouteBuilder app)
    {
        // 1) Start the OIDC dance.
        app.MapGet("/auth/sso/login", (HttpContext ctx) =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = "/auth/sso/complete" },
                new[] { "OpenIdConnect" }));

        // 2) After the OIDC middleware validates the token (via /signin-oidc), it lands here
        //    with the transient external cookie principal.
        app.MapGet("/auth/sso/complete",
            async (HttpContext ctx, SsoCallbackService callback, OneTimeCodeStore codes, IConfiguration cfg) =>
        {
            var spa = cfg["Spa:BaseUrl"]!.TrimEnd('/');
            var email = ctx.User.FindFirstValue("preferred_username")
                     ?? ctx.User.FindFirstValue(ClaimTypes.Email)
                     ?? ctx.User.FindFirstValue(ClaimTypes.Upn)
                     ?? ctx.User.Identity?.Name;

            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (string.IsNullOrWhiteSpace(email))
                return Results.Redirect($"{spa}/auth/callback?error=denied");

            var res = await callback.BuildAuthResponseAsync(email);
            if (res is null)
                return Results.Redirect($"{spa}/auth/callback?error=denied");

            var code = codes.Issue(res);
            return Results.Redirect($"{spa}/auth/callback?code={code}");
        });

        // 3) SPA exchanges the one-time code for the JWT payload.
        app.MapPost("/auth/sso/exchange", ([FromBody] ExchangeRequest body, OneTimeCodeStore codes) =>
            codes.TryConsume(body.Code, out var res) && res is not null
                ? Results.Ok(res)
                : Results.BadRequest(new { message = "Code invalide ou expiré." }));

        // 4) Sign out of Azure AD.
        app.MapGet("/auth/sso/logout", () =>
            Results.SignOut(new AuthenticationProperties { RedirectUri = "/" },
                new[] { "OpenIdConnect", CookieAuthenticationDefaults.AuthenticationScheme }));

        return app;
    }

    public record ExchangeRequest(string Code);
}
```

Register in `Program.cs` (near the other `AddScoped` auth services and after `app.Build()` where endpoints are mapped):

```csharp
builder.Services.AddScoped<SsoCallbackService>();
builder.Services.AddSingleton<OneTimeCodeStore>();
// ... after var app = builder.Build(); alongside app.MapAuthEndpoints():
app.MapSsoEndpoints();
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Ecare.Api.Tests --filter OneTimeCodeStoreTests` and `dotnet test tests/Ecare.Application.Tests --filter SsoCallbackServiceTests`
Expected: PASS. Then `dotnet build src/Ecare.Api/Ecare.Api.csproj`.

- [ ] **Step 5: Commit**

```bash
git add src/Ecare.Application/Auth/Services/SsoCallbackService.cs src/Ecare.Api/Security/OneTimeCodeStore.cs src/Ecare.Api/Endpoints/SsoEndpoints.cs src/Ecare.Api/Program.cs tests/Ecare.Api.Tests/OneTimeCodeStoreTests.cs tests/Ecare.Application.Tests/SsoCallbackServiceTests.cs tests/Ecare.Application.Tests/Ecare.Application.Tests.csproj
git commit -m "feat(auth): SSO endpoints, one-time-code store, and callback service"
```

---

## Task 7: Gate password login behind PasswordLoginEnabled

**Files:**
- Modify: `src/Ecare.Api/Endpoints/AuthEndpoints.cs`

**Interfaces:**
- Consumes: `Auth:PasswordLoginEnabled` (Task 4 config).

- [ ] **Step 1: Wrap the `/auth/login` handler**

At the top of the `/auth/login` handler, inject `IConfiguration cfg` and short-circuit when disabled:

```csharp
app.MapPost("/auth/login", async (LoginQuery query, IMediator mediator, IConfiguration cfg, ILoggerFactory loggerFactory) =>
{
    if (!cfg.GetValue("Auth:PasswordLoginEnabled", true))
        return Results.Json(new { message = "La connexion par mot de passe est désactivée. Utilisez le SSO." },
            statusCode: StatusCodes.Status403Forbidden);

    var logger = loggerFactory.CreateLogger("AuthLogin");
    // ... existing try/catch unchanged ...
});
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Ecare.Api/Ecare.Api.csproj`
Expected: succeeds. (Default `true` → behavior unchanged today.)

- [ ] **Step 3: Commit**

```bash
git add src/Ecare.Api/Endpoints/AuthEndpoints.cs
git commit -m "feat(auth): gate password login behind Auth:PasswordLoginEnabled (default on)"
```

---

## Task 8: Enforce permissions on agent/admin endpoints (with kiosk carve-out)

**Files:**
- Modify: the agent/admin endpoint files under `src/Ecare.Api/Endpoints/` (`OrderEndpoints.cs`, `QueueEndpoints.cs`, `FluxEndpoints.cs`, `PabEndpoints.cs`, `DeviceEndpoints.cs`, `LigneEndpoints.cs`, `OtherEndpoints.cs` — actual names per the folder)
- Reference only (do NOT protect): `KioskEndpoints.cs` and any SignalR hub map — kiosk-hardware carve-out.

**Interfaces:**
- Consumes: `Perm:<key>` policies (Tasks 3–4).

> Enforcement is applied by adding `.RequireAuthorization("Perm:<key>")` to each agent-facing route (or to a `MapGroup(...)` when a whole group shares one permission). Read-endpoints get the `.Read` permission; mutating endpoints get the `.Write`/`.Execute` permission. Because password tokens are exempt in the handler, existing password users are unaffected; only SSO users are gated.

**Route → permission mapping (apply verbatim; adjust route strings to the actual handlers as you read each file):**

| Endpoint area | Kind | Policy |
|---|---|---|
| Order create / validate / cancel | write | `Perm:Commands.Write` |
| Order quantity correction | write | `Perm:CommandsQuantities.Write` |
| Order read/list | read | `Perm:Commands.Read` |
| Flux status read | read | `Perm:FluxStatus.Read` |
| Queue force-call | write | `Perm:ForceCall.Execute` |
| Queue read | read | `Perm:ForceCall.Read` |
| Camion / chauffeur create/update (logistics data) | write | `Perm:LogisticsData.Write` |
| Camion / chauffeur read | read | `Perm:LogisticsData.Read` |
| RFID / SLV affectation write | write | `Perm:Rfid.Write` |
| RFID / SLV read | read | `Perm:Rfid.Read` |
| Ensachage (articles/postes) read | read | `Perm:Bagging.Read` |
| Ensachage write | write | `Perm:Bagging.Write` |
| Loading-point quotas read | read | `Perm:LoadingQuotas.Read` |
| Loading-point quotas write | write | `Perm:LoadingQuotas.Write` |
| Reports journalier | read | `Perm:Reports.Read` |
| Pesage / pont-bascule read | read | `Perm:Weighing.Read` |
| Traceability logs | read | `Perm:TraceabilityLogs.Read` |
| Loading settings (plombe / nb sac) write | write | `Perm:LoadingSettings.Write` |
| Loading settings read | read | `Perm:LoadingSettings.Read` |

- [ ] **Step 1: Apply the pattern to one file first (OrderEndpoints)**

Example (write route + read route):

```csharp
app.MapPost("/orders", async (CreateOrderCommand cmd, IMediator m) => Results.Ok(await m.Send(cmd)))
   .RequireAuthorization("Perm:Commands.Write");

app.MapGet("/orders", async ([AsParameters] GetOrdersQuery q, IMediator m) => Results.Ok(await m.Send(q)))
   .RequireAuthorization("Perm:Commands.Read");
```

- [ ] **Step 2: Apply to the remaining agent/admin endpoint files** per the mapping table. Leave `KioskEndpoints.cs`, `/login`, `/scanner`, the cardholder→pesage kiosk journey, and SignalR hub maps **untouched** (carve-out).

- [ ] **Step 3: Verify build + manual carve-out check**

Run: `dotnet build src/Ecare.Api/Ecare.Api.csproj`
Expected: succeeds. Boot locally; confirm (a) a call with **no token** to `/orders` returns 401; (b) the kiosk journey endpoints still respond without a token (carve-out intact). Record these two checks in the PR description (this is the Review-Focus "anonymous request" line — verified manually here since these are integration-level).

- [ ] **Step 4: Commit**

```bash
git add src/Ecare.Api/Endpoints
git commit -m "feat(security): enforce RBAC permissions on agent/admin endpoints (kiosk carve-out)"
```

---

## Task 9: SQL script to add the roles + activité links (shared DB)

**Files:**
- Create: `docs/specs/sql/2026-09-24-ecare-roles.sql`

**Interfaces:** none (ops artifact). Idempotent; safe to re-run.

- [ ] **Step 1: Write the script**

```sql
-- Adds the 3 new Ecare profil roles (Admin IT already exists) and links all four
-- to the Ciments activité so they appear in mycimar's GetRolesParActivité dropdown.
-- Idempotent. Run against the shared DB (sqldb-emea-we-dssprod-dss-001). No EF migration.

SET NOCOUNT ON;

DECLARE @roles TABLE (Name NVARCHAR(256));
INSERT INTO @roles (Name) VALUES (N'Agent de Guichet'), (N'Logistique'), (N'Expédition');

INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
SELECT LOWER(CONVERT(NVARCHAR(36), NEWID())), r.Name, UPPER(r.Name), CONVERT(NVARCHAR(36), NEWID())
FROM @roles r
WHERE NOT EXISTS (SELECT 1 FROM AspNetRoles a WHERE a.NormalizedName = UPPER(r.Name));

-- Link the four profil roles to the Ciments activité (if the Activite/link tables exist).
DECLARE @ciment INT = (SELECT TOP 1 Id FROM Activite WHERE Service LIKE N'Ciment%');

IF @ciment IS NOT NULL
BEGIN
    INSERT INTO AspNetRoles_Activités (Role_Id, Activité_Id)
    SELECT a.Id, @ciment
    FROM AspNetRoles a
    WHERE a.NormalizedName IN (UPPER(N'Agent de Guichet'), UPPER(N'Logistique'),
                               UPPER(N'Expédition'), UPPER(N'Admin IT'))
      AND NOT EXISTS (SELECT 1 FROM AspNetRoles_Activités x
                      WHERE x.Role_Id = a.Id AND x.Activité_Id = @ciment);
END
```

- [ ] **Step 2: Dry-review**

Confirm the real column names of `AspNetRoles_Activités` (`Role_Id`, `Activité_Id`) against `mycimar-web-client/src/Domain/Entities/AspNetRoles_Activités.cs` before running in any environment. Adjust the `Service LIKE` filter if the activité label differs.

- [ ] **Step 3: Commit**

```bash
git add docs/specs/sql/2026-09-24-ecare-roles.sql
git commit -m "chore(sql): idempotent script to add Ecare profil roles + Ciments activité links"
```

---

## Task 10: Frontend test harness (Vitest)

**Files:**
- Modify: `Ecare-FrontUi/package.json` (devDeps + `test` script)
- Create: `Ecare-FrontUi/vitest.config.ts`
- Create: `Ecare-FrontUi/src/test/setup.ts`

> Ecare-FrontUi had no JS test runner. This task adds Vitest (+ Testing Library + jsdom) so the
> following frontend tasks are TDD like the backend. Vitest is Vite-native, so it reuses the existing
> Vite config with zero extra bundler setup.

**Interfaces:**
- Produces: `npm run test` (Vitest), jsdom environment, `@testing-library/jest-dom` matchers.

- [ ] **Step 1: Install dev dependencies**

```bash
cd Ecare-FrontUi
npm i -D vitest@2 jsdom @testing-library/react @testing-library/jest-dom @testing-library/user-event
```

- [ ] **Step 2: Add the test script to `package.json`**

```json
"scripts": {
  "test": "vitest run",
  "test:watch": "vitest"
}
```

- [ ] **Step 3: Create `vitest.config.ts`**

```ts
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
```

(If the project's Vite React plugin import path differs, match `vite.config.ts`.)

- [ ] **Step 4: Create `src/test/setup.ts`**

```ts
import '@testing-library/jest-dom'
```

- [ ] **Step 5: Sanity test**

Create `src/test/smoke.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
describe('harness', () => { it('runs', () => { expect(1 + 1).toBe(2) }) })
```

Run: `npm run test`
Expected: 1 passing test.

- [ ] **Step 6: Commit**

```bash
git add Ecare-FrontUi/package.json Ecare-FrontUi/package-lock.json Ecare-FrontUi/vitest.config.ts Ecare-FrontUi/src/test
git commit -m "test(fe): add Vitest + Testing Library harness"
```

---

## Task 11: Frontend — authService permissions + auth method

**Files:**
- Modify: `Ecare-FrontUi/src/services/authService.ts`
- Test: `Ecare-FrontUi/src/services/authService.test.ts`

**Interfaces:**
- Produces: `authService.getAuthMethod(): string | null`, `authService.getPermissions(): string[]`, `authService.hasPermission(key: string): boolean` (pwd ⇒ always true), `authService.setSession(payload)`, and stores `authMethod`/`perms` on the `AdminUser`.

- [ ] **Step 0: Write the failing test**

```ts
// src/services/authService.test.ts
import { describe, it, expect, beforeEach } from 'vitest'
import { authService } from './authService'

beforeEach(() => localStorage.clear())

describe('authService.hasPermission', () => {
  it('password session has every permission', () => {
    authService.setSession({ token: 't', userId: 'u1', role: 'Admin', authMethod: 'pwd', perms: [] })
    expect(authService.hasPermission('Commands.Write')).toBe(true)
  })

  it('sso session is gated by perms', () => {
    authService.setSession({ token: 't', userId: 'u1', role: 'Agent de Guichet',
      authMethod: 'sso', perms: ['Commands.Read'] })
    expect(authService.hasPermission('Commands.Read')).toBe(true)
    expect(authService.hasPermission('Commands.Write')).toBe(false)
  })

  it('no session has no permissions', () => {
    expect(authService.hasPermission('Commands.Read')).toBe(false)
  })
})
```

Run: `npm run test -- authService` → FAIL (`setSession`/`hasPermission` missing).

- [ ] **Step 1: Extend `AdminUser` + storage**

Add to the `AdminUser` interface: `authMethod?: string` and `perms?: string[]`. In `login()`, capture them from the response:

```ts
authMethod: (response as any).authMethod,
perms: (response as any).perms || [],
```

- [ ] **Step 2: Add the accessors**

```ts
getAuthMethod(): string | null {
  return this.getCurrentUser()?.authMethod ?? null
},
getPermissions(): string[] {
  return this.getCurrentUser()?.perms ?? []
},
hasPermission(key: string): boolean {
  const u = this.getCurrentUser()
  if (!u) return false
  if (u.authMethod === 'pwd') return true      // password sessions: full/legacy access
  return (u.perms ?? []).includes(key)
},
```

- [ ] **Step 3: Add a helper to store an exchange payload** (used by the callback route, Task 13):

```ts
setSession(payload: any): void {
  if (payload?.token) localStorage.setItem(TOKEN_KEY, payload.token)
  const userData: AdminUser = {
    id: payload.userId || '', userId: payload.userId || '',
    username: payload.userName, email: payload.email, role: payload.role,
    roles: payload.role ? [payload.role] : [], expiresAt: payload.expiresAt,
    authMethod: payload.authMethod, perms: payload.perms || [],
  }
  localStorage.setItem(USER_KEY, JSON.stringify(userData))
},
```

- [ ] **Step 4: Run the test + build**

Run: `cd Ecare-FrontUi && npm run test -- authService`
Expected: PASS (3 tests). Then `npm run build` — type-checks and builds.

- [ ] **Step 5: Commit**

```bash
git add Ecare-FrontUi/src/services/authService.ts Ecare-FrontUi/src/services/authService.test.ts
git commit -m "feat(fe): authService permissions + auth-method helpers"
```

---

## Task 12: Frontend — SSO login redirect, exchange call, 401 repoint

**Files:**
- Modify: `Ecare-FrontUi/src/api/auth.ts`
- Modify: `Ecare-FrontUi/src/config/env.ts` (no change to `API_BASE_URL`; reuse it)
- Test: `Ecare-FrontUi/src/api/auth.sso.test.ts`

**Interfaces:**
- Produces: `startSsoLogin()` (redirects to `${API_BASE_URL}/auth/sso/login`), `ssoExchange(code: string): Promise<any>` (POST `/auth/sso/exchange`).

- [ ] **Step 0: Write the failing test**

```ts
// src/api/auth.sso.test.ts
import { describe, it, expect, vi, afterEach } from 'vitest'
import { ssoExchange } from './auth'

afterEach(() => vi.restoreAllMocks())

describe('ssoExchange', () => {
  it('posts the code and returns the payload', async () => {
    const payload = { token: 't', role: 'Agent de Guichet', authMethod: 'sso', perms: ['Commands.Read'] }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true, json: () => Promise.resolve(payload),
    }))
    const res = await ssoExchange('abc')
    expect(res.role).toBe('Agent de Guichet')
    expect((fetch as any).mock.calls[0][0]).toContain('/auth/sso/exchange')
  })

  it('throws on non-ok response', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false }))
    await expect(ssoExchange('bad')).rejects.toThrow()
  })
})
```

Run: `npm run test -- auth.sso` → FAIL (`ssoExchange` missing).

- [ ] **Step 1: Add SSO helpers to `api/auth.ts`**

```ts
import { API_BASE_URL } from '../config/env'

export function startSsoLogin(): void {
  window.location.href = `${API_BASE_URL}/auth/sso/login`
}

export async function ssoExchange(code: string): Promise<any> {
  const res = await fetch(`${API_BASE_URL}/auth/sso/exchange`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ code }),
  })
  if (!res.ok) throw new Error('exchange_failed')
  return res.json()
}
```

- [ ] **Step 2: Run the test + build**

Run: `cd Ecare-FrontUi && npm run test -- auth.sso`
Expected: PASS (2 tests). Then `npm run build` — builds.

- [ ] **Step 3: Commit**

```bash
git add Ecare-FrontUi/src/api/auth.ts Ecare-FrontUi/src/api/auth.sso.test.ts
git commit -m "feat(fe): SSO login redirect + one-time-code exchange client"
```

---

## Task 13: Frontend — add "Se connecter avec SSO" to the login page

**Files:**
- Modify: `Ecare-FrontUi/src/features/Admin/auth/AdminLoginPage.tsx`

**Interfaces:**
- Consumes: `startSsoLogin` (Task 11).

- [ ] **Step 1: Add the SSO button below the password form**

```tsx
import { startSsoLogin } from '../../../api/auth'
// ...
<button type="button" className="btn btn-light-primary w-100 mt-3" onClick={startSsoLogin}>
  Se connecter avec SSO
</button>
```

Keep the existing username/password form exactly as-is (coexistence).

- [ ] **Step 2: Verify build + manual check**

Run: `cd Ecare-FrontUi && npm run build`; then `npm run dev` and confirm the button appears and navigates to the API `/auth/sso/login` (will 404/loop until AzureAd is configured — acceptable in dev without SSO config).

- [ ] **Step 3: Commit**

```bash
git add Ecare-FrontUi/src/features/Admin/auth/AdminLoginPage.tsx
git commit -m "feat(fe): add SSO login button alongside password form"
```

---

## Task 14: Frontend — /auth/callback route

**Files:**
- Create: `Ecare-FrontUi/src/features/Admin/auth/SsoCallbackPage.tsx`
- Modify: `Ecare-FrontUi/src/app/routing/PrivateRoutes.tsx` (add the route)
- Test: `Ecare-FrontUi/src/features/Admin/auth/SsoCallbackPage.test.tsx`

**Interfaces:**
- Consumes: `ssoExchange` (Task 12), `authService.setSession` (Task 11).

- [ ] **Step 0: Write the failing test**

```tsx
// src/features/Admin/auth/SsoCallbackPage.test.tsx
import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import SsoCallbackPage from './SsoCallbackPage'

vi.mock('../../../api/auth', () => ({ ssoExchange: vi.fn() }))

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes><Route path="/auth/callback" element={<SsoCallbackPage />} /></Routes>
    </MemoryRouter>
  )
}

describe('SsoCallbackPage', () => {
  it('shows an error when the provider returned error=denied', async () => {
    renderAt('/auth/callback?error=denied')
    expect(await screen.findByText(/la connexion a échoué/i)).toBeInTheDocument()
  })

  it('shows an error when no code is present', async () => {
    renderAt('/auth/callback')
    expect(await screen.findByText(/la connexion a échoué/i)).toBeInTheDocument()
  })
})
```

Run: `npm run test -- SsoCallbackPage` → FAIL (component missing).

- [ ] **Step 1: Create the callback page**

```tsx
// SsoCallbackPage.tsx
import { useEffect, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { ssoExchange } from '../../../api/auth'
import { authService } from '../../../services/authService'

export default function SsoCallbackPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const code = params.get('code')
    const err = params.get('error')
    if (err || !code) { setError('La connexion a échoué, veuillez réessayer.'); return }
    ssoExchange(code)
      .then((payload) => { authService.setSession(payload); navigate('/admin', { replace: true }) })
      .catch(() => setError('La connexion a échoué, veuillez réessayer.'))
  }, [params, navigate])

  return <div className="d-flex justify-content-center p-10">{error ?? 'Connexion en cours…'}</div>
}
```

- [ ] **Step 2: Register the route** (public, before the guarded block) in `PrivateRoutes.tsx`:

```tsx
import SsoCallbackPage from '../../features/Admin/auth/SsoCallbackPage'
// inside <Routes>, with the other public routes:
<Route path='/auth/callback' element={<SsoCallbackPage />} />
```

- [ ] **Step 3: Run the test + build**

Run: `cd Ecare-FrontUi && npm run test -- SsoCallbackPage`
Expected: PASS (2 tests). Then `npm run build` — builds.

- [ ] **Step 4: Commit**

```bash
git add Ecare-FrontUi/src/features/Admin/auth/SsoCallbackPage.tsx Ecare-FrontUi/src/features/Admin/auth/SsoCallbackPage.test.tsx Ecare-FrontUi/src/app/routing/PrivateRoutes.tsx
git commit -m "feat(fe): SSO callback route exchanges code and stores session"
```

---

## Task 15: Frontend — re-enable auth guard + permission-aware gating

**Files:**
- Create: `Ecare-FrontUi/src/app/routing/RequirePermission.tsx`
- Modify: `Ecare-FrontUi/src/app/routing/PrivateRoutes.tsx` (uncomment/enable `RequireAuth`; wrap admin routes)
- Test: `Ecare-FrontUi/src/app/routing/RequirePermission.test.tsx`

**Interfaces:**
- Consumes: `authService.isAuthenticated`, `authService.hasPermission` (Task 11).

- [ ] **Step 0: Write the failing test**

```tsx
// src/app/routing/RequirePermission.test.tsx
import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import RequirePermission from './RequirePermission'
import { authService } from '../../services/authService'

beforeEach(() => localStorage.clear())

function renderGuarded(permission: string) {
  return render(
    <MemoryRouter initialEntries={['/slv']}>
      <Routes>
        <Route element={<RequirePermission permission={permission} />}>
          <Route path="/slv" element={<div>SLV PAGE</div>} />
        </Route>
        <Route path="/login" element={<div>LOGIN</div>} />
      </Routes>
    </MemoryRouter>
  )
}

describe('RequirePermission', () => {
  it('redirects to /login when unauthenticated', () => {
    renderGuarded('Rfid.Read')
    expect(screen.getByText('LOGIN')).toBeInTheDocument()
  })

  it('renders the page when an sso session has the permission', () => {
    authService.setSession({ token: 't', userId: 'u1', role: 'Agent de Guichet',
      authMethod: 'sso', perms: ['Rfid.Read'] })
    renderGuarded('Rfid.Read')
    expect(screen.getByText('SLV PAGE')).toBeInTheDocument()
  })

  it('redirects when an sso session lacks the permission', () => {
    authService.setSession({ token: 't', userId: 'u1', role: 'Expédition',
      authMethod: 'sso', perms: ['FluxStatus.Read'] })
    renderGuarded('Rfid.Read')
    expect(screen.getByText('LOGIN')).toBeInTheDocument()
  })
})
```

Run: `npm run test -- RequirePermission` → FAIL (component missing).

- [ ] **Step 1: Create `RequirePermission`**

```tsx
import { Navigate, Outlet } from 'react-router-dom'
import { authService } from '../../services/authService'

export default function RequirePermission({ permission }: { permission: string }) {
  if (!authService.isAuthenticated()) return <Navigate to="/login" replace />
  if (!authService.hasPermission(permission)) return <Navigate to="/login" replace />
  return <Outlet />
}
```

- [ ] **Step 2: Re-enable `RequireAuth`** in `PrivateRoutes.tsx** by uncommenting the `<Route element={<RequireAuth />}>` wrapper around the protected block. Wrap the most sensitive admin routes with `RequirePermission` per the matrix (example — SLV/RFID page needs `Rfid.Read`):

```tsx
<Route element={<RequirePermission permission="Rfid.Read" />}>
  <Route path='/slv' element={<SlvCardsPage />} />
</Route>
```

Apply the analogous wrapper to `/camion`, `/chauffeur` (`LogisticsData.Read`), and loading-settings/mobile pages (`LoadingSettings.Read`). Do **not** guard the kiosk journey routes (`/cardholder`, `/order`, `/first-pesage`, …) — carve-out.

- [ ] **Step 3: Run the test + build + manual check**

Run: `cd Ecare-FrontUi && npm run test -- RequirePermission`
Expected: PASS (3 tests). Then `npm run build`; `npm run dev`. Password login → all pages reachable (pwd ⇒ hasPermission true). Confirm an unauthenticated visit to `/slv` redirects to `/login`.

- [ ] **Step 4: Commit**

```bash
git add Ecare-FrontUi/src/app/routing/RequirePermission.tsx Ecare-FrontUi/src/app/routing/RequirePermission.test.tsx Ecare-FrontUi/src/app/routing/PrivateRoutes.tsx
git commit -m "feat(fe): re-enable auth guard + permission-aware route gating"
```

---

## Final verification (before opening the PR)

- [ ] Backend: `dotnet test` across `tests/Ecare.Application.Tests` and `tests/Ecare.Api.Tests` — all green.
- [ ] Backend: `dotnet build src/Ecare.Api/Ecare.Api.csproj` — clean.
- [ ] Frontend: `cd Ecare-FrontUi && npm run test` — all Vitest suites green (authService, auth.sso, SsoCallbackPage, RequirePermission).
- [ ] Frontend: `cd Ecare-FrontUi && npm run build` — clean.
- [ ] Manual dev-environment pass (team): password login unchanged; SSO login (once AzureAd configured) yields a guichet session that can create orders but a Expédition session cannot; kiosk journey works without a token.
- [ ] Confirm no EF migration was applied to the shared DB; only Task 9 SQL run.

## Notes for deployment (from spec Section 12)

1. Run Task 9 SQL against the shared DB.
2. Register the Ecare `/signin-oidc` redirect URI in the Azure AD app; set `AzureAd__TenantID`, `AzureAd__ClientID`, `AzureAd__ClientSecret`, and `Spa__BaseUrl` in the Ecare App Service settings.
3. Admin IT assigns users to the new roles via mycimar's `Employées` pages.
4. Deploy Ecare_Kiosk, then Ecare-FrontUi (`PasswordLoginEnabled` stays `true`).
5. (Deferred) flip `Auth:PasswordLoginEnabled=false` only when retiring password login.
