using Ecare.Application.Commands.MobileCommands;
using Ecare.Application.Queries.GetFluxStagesSummary;
using Ecare.Application.Queries.MobileQueries.GetActiveChargings;
using Ecare.Application.Queries.MobileQueries.GetChargementLines;
using Ecare.Application.Queries.MobileQueries.GetFluxChargingDetails;
using Ecare.Shared;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Ecare.Api.Endpoints
{
    public static class MobileAppEndpoints
    {
        public static IEndpointRouteBuilder MapMobileAppEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/mobile")
                .WithTags("Mobile App");

            group.MapGet("/charging/active",
            async Task<Results<Ok<IDictionary<string, ChargingGroupVm>>, ProblemHttpResult>>
            ([FromQuery] string? ligne, [FromQuery] string? type,
             [FromServices] IMediator mediator, CancellationToken ct) =>
            {
                var res = await mediator.Send(new GetActiveChargingsByMatriculeQuery(ligne, type), ct);
                if (!res.Success)
                    return TypedResults.Problem(title: "Failed to load active chargings", detail: res.Error);

                return TypedResults.Ok((IDictionary<string, ChargingGroupVm>)res.Value!);
            })
            .Produces<IDictionary<string, ChargingGroupVm>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Active chargings grouped by matricule")
            .WithDescription("Returns unfinished chargings filtered optionally by Ligne and/or Type.");

            group.MapGet("/charging/details/{efId:int}",
            async Task<Results<Ok<FluxChargingGroupVm>, NotFound, ProblemHttpResult>>
            ([FromRoute] int efId, [FromServices] IMediator mediator, CancellationToken ct) =>
            {
                var res = await mediator.Send(new GetFluxChargingDetailsByIdQuery(efId), ct);

                if (!res.Success)
                    return TypedResults.Problem(title: "Failed to load flux charging details", detail: res.Error);

                if (res.Value is null)
                    return TypedResults.NotFound();

                return TypedResults.Ok(res.Value);
            })
            .Produces<FluxChargingGroupVm>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Charging details for a single EcareFlux row")
            .WithDescription("Returns grouped product items and status for the specified EcareFlux Id.");

            group.MapGet("/chargement",
            async Task<Results<Ok<IReadOnlyList<ChargementLineVm>>, ProblemHttpResult>>
            ([FromQuery] string type, [FromServices] IMediator mediator, CancellationToken ct) =>
            {
                var res = await mediator.Send(new GetChargementLinesByTypeQuery(type), ct);

                if (!res.Success)
                    return TypedResults.Problem(title: "Failed to load chargement lines", detail: res.Error);

                return TypedResults.Ok(res.Value!);
            })
            .Produces<IReadOnlyList<ChargementLineVm>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Get chargement lines by type")
            .WithDescription("Returns all Ecare_Ligne rows where the zone's TypeOperation matches the specified type (e.g., VRAC, SAC, etc.).");

            group.MapPut("/bags",
            async Task<Results<Ok<Result<int>>, ProblemHttpResult>>
            ([FromBody] UpdateFluxBagsCommand command,
             [FromServices] IMediator mediator,
             CancellationToken ct) =>
            {
                var res = await mediator.Send(command, ct);

                if (!res.Success)
                    return TypedResults.Problem(title: "Failed to update Flux bags", detail: res.Error);

                return TypedResults.Ok(res);
            })
            .Produces<Result<int>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Update MinusBag and PlusBag for a flux entry")
            .WithDescription("Updates the EcareFlux row with new MinusBag and PlusBag values based on Id.");

            group.MapGet("/stages",
            async Task<Results<Ok<IReadOnlyList<FluxStageSummary>>, ProblemHttpResult>>
            ([FromQuery] string? type,
             [FromServices] IMediator mediator,
             CancellationToken ct) =>
            {
                var res = await mediator.Send(new GetFluxStagesSummaryQuery(type), ct);

                if (!res.Success)
                    return TypedResults.Problem(title: "Failed to load flux stage summary", detail: res.Error);

                return TypedResults.Ok(res.Value!);
            })
            .Produces<IReadOnlyList<FluxStageSummary>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Get grouped flux stages (PARC, USINE, CHARGEMENT, SORTIE)")
            .WithDescription("Groups trucks by stage with timing details and aggregates, optionally filtered by product Type.");

            return app;
        }
    }
}
