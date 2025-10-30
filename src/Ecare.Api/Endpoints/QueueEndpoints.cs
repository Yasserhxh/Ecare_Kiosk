using Ecare.Application.Commands.Queue.CreateQueue;
using Ecare.Application.Commands.Queue.PinTruck;
using Ecare.Application.Commands.Queue.UpdateQueue;
using Ecare.Application.Services.Queue;
using Ecare.Shared;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.SignalR.Management;

namespace Ecare.Api.Endpoints;

public static class QueueEndpoints
{
    public static IEndpointRouteBuilder MapQueueEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/queue", async ([FromBody] CreateQueueEntryCommand cmd, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(cmd, ct);
            if (!result.Success)
                return Results.BadRequest(new { error = result.Error });

            return Results.Created($"/queue/{result.Value}", new { id = result.Value });
        })
        .WithName("CreateQueueEntry");

        app.MapPost("/queue/toggle-pin/{matricule}", async (string matricule, IMediator mediator, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(matricule))
                return Results.BadRequest("matricule is required");

            var res = await mediator.Send(new TogglePinByMatriculeCommand(matricule), ct);
            return res.Success ? Results.Ok(new { affected = res.Value }) : Results.BadRequest(res.Error);
        });

        app.MapPost("/queue/rebroadcast", async (ServiceManager signalR, IUnitOfWork uow, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var log = loggerFactory.CreateLogger("QueueRebroadcast");
            await QueueSnapshot.BuildAndBroadcastAsync(signalR, uow, log, ct);
            return Results.Ok(new { ok = true, sent = "QueueDataEvent", hub = "queue_data_hub" });
        });

        app.MapPost("/queue/update-details", async (UpdateQueueDetailsCommand c, IMediator m) =>
            await m.Send(c));

        return app;
    }
}