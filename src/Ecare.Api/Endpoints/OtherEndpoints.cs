using Ecare.Application.Commands;
using Ecare.Application.Queries;
using MediatR;
using Microsoft.Azure.SignalR.Management;

namespace Ecare.Api.Endpoints;

public static class OtherEndpoints
{
    public static IEndpointRouteBuilder MapOtherEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/line/start", async (StartLoadingCommand c, IMediator m) =>
            await m.Send(c));

        app.MapGet("/catalog/items", async (IMediator m, CancellationToken ct) =>
            await m.Send(new GetCimentsQuery(), ct));

        // SignalR negotiate endpoint
        app.MapGet("/signalr/negotiate", async (string hub, ServiceManager manager, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(hub))
                return Results.BadRequest("hub is required");

            await using var hubContext = await manager.CreateHubContextAsync(hub, ct);
            var negotiation = await hubContext.NegotiateAsync(new NegotiationOptions(), ct);
            return Results.Ok(new { url = negotiation.Url, accessToken = negotiation.AccessToken });
        });

        return app;
    }
}