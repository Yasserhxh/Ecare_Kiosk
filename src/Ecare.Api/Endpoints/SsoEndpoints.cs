using System.Security.Claims;
using Ecare.Api.Security;
using Ecare.Application.Auth.Services;
using Microsoft.AspNetCore.Authentication;
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
        //    I1: AuthenticateAsync is wrapped in try/catch; authResult.Succeeded is checked before
        //    extracting claims — any auth failure (scheme not registered, token tampered, etc.) redirects
        //    to ?error=denied rather than throwing a 500.
        app.MapGet("/auth/sso/complete",
            async (HttpContext ctx, SsoCallbackService callback, OneTimeCodeStore codes, IConfiguration cfg) =>
        {
            // I2: spaBase was validated at startup (see Program.cs MapSsoEndpoints call); safe to use here.
            var spa = cfg["Spa:BaseUrl"]!.TrimEnd('/');

            AuthenticateResult? authResult;
            try
            {
                // Read the principal from the transient external cookie scheme set by OIDC middleware.
                authResult = await ctx.AuthenticateAsync("EcareSsoCookie");
            }
            catch
            {
                // AuthenticateAsync can throw if the scheme isn't registered (e.g. SSO disabled at runtime).
                // Never issue a code on a failed/denied principal — redirect to denied.
                return Results.Redirect($"{spa}/auth/callback?error=denied");
            }

            // Guard: reject if authentication was not successful (no cookie, expired, tampered, etc.).
            if (authResult is null || !authResult.Succeeded)
            {
                // Attempt a best-effort sign-out to clear any partial state, ignoring errors.
                try { await ctx.SignOutAsync("EcareSsoCookie"); } catch { /* ignore */ }
                return Results.Redirect($"{spa}/auth/callback?error=denied");
            }

            var principal = authResult.Principal;

            // Fallback claim chain: preferred_username → email → UPN → Identity.Name
            var email = principal?.FindFirstValue("preferred_username")
                     ?? principal?.FindFirstValue(ClaimTypes.Email)
                     ?? principal?.FindFirstValue(ClaimTypes.Upn)
                     ?? principal?.Identity?.Name;

            // Clean up the external cookie — it is single-use for this exchange.
            try { await ctx.SignOutAsync("EcareSsoCookie"); } catch { /* ignore */ }

            if (string.IsNullOrWhiteSpace(email))
                return Results.Redirect($"{spa}/auth/callback?error=denied");

            var res = await callback.BuildAuthResponseAsync(email);
            if (res is null)
                return Results.Redirect($"{spa}/auth/callback?error=denied");

            // Invariant preserved: code is only issued when authResult.Succeeded and email resolved.
            var code = codes.Issue(res);
            return Results.Redirect($"{spa}/auth/callback?code={code}");
        });

        // 3) SPA exchanges the one-time code for the JWT payload.
        app.MapPost("/auth/sso/exchange", ([FromBody] ExchangeRequest body, OneTimeCodeStore codes) =>
            codes.TryConsume(body.Code, out var res) && res is not null
                ? Results.Ok(res)
                : Results.BadRequest(new { message = "Code invalide ou expiré." }));

        // 4) Sign out of Azure AD and clear the SSO external cookie.
        //    m2: Sign out "EcareSsoCookie" (the SSO external cookie set by OIDC middleware) — NOT
        //    the default "Cookies" scheme which is unrelated to SSO.  "OpenIdConnect" triggers the
        //    Azure AD front-channel logout; "EcareSsoCookie" clears the local transient cookie.
        app.MapGet("/auth/sso/logout", () =>
            Results.SignOut(new AuthenticationProperties { RedirectUri = "/" },
                new[] { "OpenIdConnect", "EcareSsoCookie" }));

        return app;
    }

    public record ExchangeRequest(string Code);
}
