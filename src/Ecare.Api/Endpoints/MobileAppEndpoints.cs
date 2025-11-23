using Ecare.Application.Commands.MobileCommands;
using Ecare.Application.Queries.MobileQueries.GetActiveChargings;
using Ecare.Application.Queries.MobileQueries.GetChargementLines;
using Ecare.Application.Queries.MobileQueries.GetFluxChargingDetails;
using Ecare.Shared;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using FluxStageSummary = Ecare.Application.Queries.MobileQueries.GetFluxChargingDetails.FluxStageSummary;

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

            group.MapGet("/charging/details",
            async Task<Results<Ok<IReadOnlyList<FluxStageSummary>>, NotFound, ProblemHttpResult>>
            ([FromQuery] string? type,
             [FromServices] IMediator mediator,
             CancellationToken ct) =>
            {
                var res = await mediator.Send(new GetFluxChargingDetailsQuerie(type), ct);

                if (!res.Success)
                    return TypedResults.Problem(
                        title: "Failed to load flux charging details",
                        detail: res.Error);

                // res.Value ALWAYS has 4 stages, but may be empty if DB returns no rows
                if (res.Value is null || res.Value.Count == 0)
                    return TypedResults.NotFound();

                return TypedResults.Ok(res.Value);
            })
            .Produces<IReadOnlyList<FluxStageSummary>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Get Flux Charging Stage Summary")
            .WithDescription("Returns the 4 charging stages (PARC, USINE, CHARGEMENT, SORTIE) with durations, optionally filtered by Type (VRAC | SAC).");




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



            group.MapGet("/dashboard",
            async Task<IResult> (
                [FromQuery] string? type,
                [FromQuery] DateTime? dateFrom,
                [FromQuery] DateTime? dateTo,
                [FromServices] IMediator mediator,
                CancellationToken ct) =>
            {
                var res = await mediator.Send(
                    new GetFluxChargingDetailsQuerie(type, dateFrom, dateTo),
                    ct);

                if (!res.Success)
                {
                    // 500 – internal error
                    return Results.Problem(
                        title: "Failed to load flux charging details",
                        detail: res.Error,
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                // 200 – OK
                return Results.Ok(res.Value);
            })
            .Produces<IReadOnlyList<FluxStageSummary>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Get flux charging stages summary")
            .WithDescription("Returns the 4 stages (PARC, USINE, CHARGEMENT, SORTIE) with durations, filtered optionally by Type and date range (ParkedAt).");



            return app;
        }
    }
}
