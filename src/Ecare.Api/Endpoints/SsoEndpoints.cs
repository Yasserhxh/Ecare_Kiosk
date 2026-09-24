using System.Security.Claims;
using Ecare.Api.Security;
using Ecare.Application.Auth.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Ecare.Api.Endpoints;

public static class SsoEndpoints
{
    public static IEndpointRouteBuilder MapSsoEndpoints(this IEndpointRouteBuilder app)
    {
        // 1) Start the OIDC dance — challenge the "OpenIdConnect" scheme.
        app.MapGet("/auth/sso/login", (HttpContext ctx) =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = "/auth/sso/complete" },
                new[] { "OpenIdConnect" }));

        // 2) After the OIDC middleware validates the ID token (via /signin-oidc), it redirects here.
        //    The external identity is stored in the "EcareSsoCookie" transient cookie scheme, NOT in
        //    ctx.User (which resolves via the default JWT bearer scheme and is empty at this point).
        //    We must authenticate explicitly against that scheme.
        app.MapGet("/auth/sso/complete",
            async (HttpContext ctx, SsoCallbackService callback, OneTimeCodeStore codes, IConfiguration cfg) =>
        {
            var spa = cfg["Spa:BaseUrl"]!.TrimEnd('/');

            // Read the principal from the transient external cookie scheme set by OIDC middleware.
            var authResult = await ctx.AuthenticateAsync("EcareSsoCookie");
            var principal = authResult?.Principal;

            // Fallback claim chain: preferred_username → email → UPN → Identity.Name
            var email = principal?.FindFirstValue("preferred_username")
                     ?? principal?.FindFirstValue(ClaimTypes.Email)
                     ?? principal?.FindFirstValue(ClaimTypes.Upn)
                     ?? principal?.Identity?.Name;

            // Clean up the external cookie — it is single-use for this exchange.
            await ctx.SignOutAsync("EcareSsoCookie");

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

        // 4) Sign out of Azure AD and clear the local cookie.
        app.MapGet("/auth/sso/logout", () =>
            Results.SignOut(new AuthenticationProperties { RedirectUri = "/" },
                new[] { "OpenIdConnect", CookieAuthenticationDefaults.AuthenticationScheme }));

        return app;
    }

    public record ExchangeRequest(string Code);
}
