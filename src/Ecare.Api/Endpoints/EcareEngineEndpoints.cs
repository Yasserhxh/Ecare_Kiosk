using Ecare.Application.Engines.Create;
using MediatR;

namespace Ecare.Api.Endpoints;

public static class EcareEngineEndpoints
{
    public static IEndpointRouteBuilder MapEcareEngineEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/engines");

        // Body is directly bound to CreateEcareEngineCommand
        group.MapPost("/", async (CreateEcareEngineCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);

            if (result.Success)
                return Results.Created($"/api/engines/{result.Value}", new { matricule = result.Value });

            return Results.BadRequest(new { error = result.Error });
        });

        return app;
    }
}
