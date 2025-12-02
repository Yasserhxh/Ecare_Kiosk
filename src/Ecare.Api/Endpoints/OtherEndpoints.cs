using Ecare.Application.Commands;
using Ecare.Application.Commands.CreateClientEquipement;
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
        app.MapGet("/signalr/negotiate", async (
        string hub,
        string? deviceId,
        ServiceManager manager,
        CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(hub))
                return Results.BadRequest("hub is required");

            //if (string.IsNullOrWhiteSpace(deviceId))
            //    return Results.BadRequest("deviceId is required");

            await using var hubContext = await manager.CreateHubContextAsync(hub, ct);

            var negotiation = await hubContext.NegotiateAsync(new NegotiationOptions
            {
                UserId = deviceId
            }, ct);

            //Get the connection ID and add it to the device group
            // Note: This won't work directly because we don't have the connectionId yet
            // The client needs to join the group after connecting

            return Results.Ok(new
            {
                url = negotiation.Url,
                accessToken = negotiation.AccessToken,
                deviceId = deviceId
            });
        });


        app.MapPost("/api/client-equipements",
        async (CreateClientEquipementDto dto, IMediator mediator) =>
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(dto.Matricule))
                return Results.BadRequest(new { error = "Matricule is required" });

            if (string.IsNullOrWhiteSpace(dto.CarteSLV))
                return Results.BadRequest(new { error = "CarteSLV is required" });

            var id = await mediator.Send(new CreateClientEquipementCommand(dto));

            return Results.Ok(new
            {
                Id = id,
                Message = "Client equipement created successfully."
            });
        });


        return app;
    }
}