using Ecare.Application.Commands.StartLoading;
using Ecare.Application.Queries;
using Ecare.Infrastructure.Repositories;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Ecare.Api.Endpoints;

public sealed record DeviceAdminKey(string Value);

public static class OtherEndpoints
{
    public static IEndpointRouteBuilder MapOtherEndpoints(this IEndpointRouteBuilder app)
    {
        // --- keep your existing endpoints ---
        app.MapPost("/line/start", async (StartLoadingCommand c, IMediator m) => await m.Send(c));
        app.MapGet("/catalog/items", async (IMediator m, CancellationToken ct) => await m.Send(new GetCimentsQuery(), ct));

        // -------------------------
        // Device register (POST)
        // -------------------------
        app.MapPost("/api/device/register",
            (HttpContext http,
             DeviceAdminKey adminKey,
             IDeviceRegistry registry,
             RegisterReq req) =>
            {
                if (!http.Request.Headers.TryGetValue("X-Api-Key", out var key) || key != adminKey.Value)
                    return Results.Unauthorized();

                if (string.IsNullOrWhiteSpace(req.DeviceId) || string.IsNullOrWhiteSpace(req.SecretHex))
                    return Results.BadRequest("Missing fields");

                byte[] secret;
                try { secret = Convert.FromHexString(req.SecretHex); }
                catch { return Results.BadRequest("Invalid secret"); }

                registry.RegisterOrUpdate(req.DeviceId, secret);
                return Results.Ok(new { ok = true });
            });

        // -------------------------
        // Negotiate (GET) with dynamic hub + device proof
        // GET /signalr/negotiate?hub=slv_hub&deviceId=...&ts=...&nonce=...&sig=HEX
        // -------------------------
        const int SkewSeconds = 120;

        app.MapGet("/signalr/negotiate",
            async (HttpContext http,
                    IServiceManager manager,      // <— concrete type you actually registered
                    IDeviceRegistry registry,
                    INonceStore nonces) =>
            {
                // bind query
                var hub = http.Request.Query["hub"].ToString();
                var deviceId = http.Request.Query["deviceId"].ToString();
                var nonce = http.Request.Query["nonce"].ToString();
                var sig = http.Request.Query["sig"].ToString();
                var tsOk = long.TryParse(http.Request.Query["ts"], out var ts);

                // validate presence
                if (string.IsNullOrWhiteSpace(hub))
                    return Results.BadRequest("hub is required");
                if (string.IsNullOrWhiteSpace(deviceId) ||
                    string.IsNullOrWhiteSpace(nonce) ||
                    string.IsNullOrWhiteSpace(sig) ||
                    !tsOk || ts == 0)
                    return Results.BadRequest("Missing device proof");

                // lookup secret
                if (!registry.TryGetSecret(deviceId, out var secret))
                    return Results.Unauthorized();

                // freshness + replay
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (Math.Abs(now - ts) > SkewSeconds)
                    return Results.Unauthorized();
                if (!nonces.TryMarkSeen(deviceId, nonce))
                    return Results.Unauthorized();

                // verify HMAC: HMACSHA256(secret, $"{deviceId}.{ts}.{nonce}")
                var msg = $"{deviceId}.{ts}.{nonce}";
                using var h = new HMACSHA256(secret);
                var expected = h.ComputeHash(Encoding.UTF8.GetBytes(msg));

                byte[] provided;
                try { provided = Convert.FromHexString(sig); }
                catch { return Results.Unauthorized(); }

                if (!CryptographicOperations.FixedTimeEquals(expected, provided))
                    return Results.Unauthorized();

                // mint URL + token for the requested hub (userId = deviceId)
                var url = manager.GetClientEndpoint(hub);
                var token = manager.GenerateClientAccessToken(
                    hub,
                    deviceId,
                    new List<Claim> { new("deviceId", deviceId) });

                // ensure the user is in its per-device group on that hub
                await using var hubCtx = await manager.CreateHubContextAsync(hub);
                var group = $"device:{deviceId}";
                await hubCtx.UserGroups.AddToGroupAsync(deviceId, group);

                return Results.Ok(new { url, accessToken = token, group, hub });
            });

        return app;
    }

    public readonly record struct NegotiateQuery(string deviceId, long ts, string nonce, string sig);
    public sealed record RegisterReq(string DeviceId, string SecretHex);
}
