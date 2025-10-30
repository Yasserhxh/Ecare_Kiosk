using Ecare.Application.Commands.Pab1Weigh;
using Ecare.Application.Commands.Pab2WeighAndBl;
using Ecare.Application.Queries;
using MediatR;

namespace Ecare.Api.Endpoints;

public static class PabEndpoints
{
    public static IEndpointRouteBuilder MapPabEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/pab1/weigh", async (RecordPab1WeighCommand c, IMediator m) =>
            await m.Send(c));

        app.MapPost("/pab2/weigh-bl", async (RecordPab2AndIssueBlCommand c, IMediator m) =>
            await m.Send(c));

        app.MapGet("/pab/exit", async Task<IResult> (string slv, ISender mediator, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(slv))
                return Results.BadRequest("Missing query parameter 'slv'.");

            var result = await mediator.Send(new GetPabExitDataQuery(slv), ct);
            if (!result.Success)
            {
                var msg = result.Error ?? "Unknown error";
                if (msg.Contains("inconnue", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("inactive", StringComparison.OrdinalIgnoreCase))
                    return Results.NotFound(msg);

                return Results.BadRequest(msg);
            }

            return Results.Ok(new { message = "Exit data retrieved and broadcast.", data = result.Value });
        });

        return app;
    }
}