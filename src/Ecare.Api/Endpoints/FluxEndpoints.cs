using Ecare.Application.Commands.Flux;
using Ecare.Application.Commands.Queue.UpdateFirstWeight;
using Ecare.Application.Commands.Queue.UpdateSecondWeight;
using Ecare.Application.Queries;
using Ecare.Application.Queries.Loading.UpdateStartLoading;
using MediatR;

namespace Ecare.Api.Endpoints;

public static class FluxEndpoints
{
    public static IEndpointRouteBuilder MapFluxEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/flux/qualite", async (IMediator m, CancellationToken ct) =>
            await m.Send(new GetFluxQualiteQuery(), ct));

        app.MapPost("/flux", async (CreateFluxEntryCommand c, IMediator m, CancellationToken ct) =>
            await m.Send(c, ct));

        app.MapPost("/flux/first-weight/by-bon", async (UpdateFirstWeightByBonCommand cmd, IMediator mediator, CancellationToken ct) =>
        {
            var res = await mediator.Send(cmd, ct);
            return res.Success
                ? Results.Ok(new { updated = res.Value })
                : Results.BadRequest(new { error = res.Error });
        });

        app.MapPost("/flux/second-weight/by-bon", async (UpdateSecondWeightByBonCommand cmd, IMediator mediator, CancellationToken ct) =>
        {
            var res = await mediator.Send(cmd, ct);
            return res.Success
                ? Results.Ok(new { updated = res.Value })
                : Results.BadRequest(new { error = res.Error });
        });

        app.MapPost("/flux/start-charging", async (
            UpdateStartChargingCommand cmd,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var res = await mediator.Send(cmd, ct);
            return res.Success
                ? Results.Ok(new { updated = res.Value })
                : Results.BadRequest(new { error = res.Error });
        })
        .WithName("Flux_UpdateStartCharging")
        .WithSummary("Met à jour EcareFlux.StartChargingAt=DateTime.Now pour un Matricule + BonDeCommande.");

        app.MapPost("/flux/finish-charging", async (
            UpdateFinishChargingCommand cmd,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var res = await mediator.Send(cmd, ct);
            return res.Success
                ? Results.Ok(new { updated = res.Value })
                : Results.BadRequest(new { error = res.Error });
        })
        .WithName("Flux_UpdateFinishCharging")
        .WithSummary("Met à jour EcareFlux.FinishChargingAt=DateTime.Now pour un Matricule + BonDeCommande.");

        return app;
    }
}