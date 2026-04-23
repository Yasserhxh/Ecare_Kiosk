using Ecare.Application.Commands.EcareDriver;
using Ecare.Application.Queries.GetDrivers;
using MediatR;

namespace Ecare.Api.Endpoints
{
    public static class DriversEndpoints
    {
        public static IEndpointRouteBuilder MapDriversEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/drivers").WithTags("Drivers");

            // -----------------------------
            // POST /api/drivers  (Create)
            // -----------------------------
            group.MapPost("/", async (CreateDriverCommand command, IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(command, ct);

                if (result.Success)
                    return Results.Created($"/api/drivers/{result.Value}", new { id = result.Value });

                return Results.BadRequest(new { error = result.Error });
            });

            // ---------------------------------------------------------
            // GET /api/drivers/dropdown  (Id + FullName, not paginated)
            // ---------------------------------------------------------
            group.MapGet("/dropdown", async (IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(new GetDriversDropdownQuery(), ct);

                return result.Success
                    ? Results.Ok(result.Value)
                    : Results.BadRequest(new { error = result.Error });
            });

            group.MapGet("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(new GetDriverByIdQuery(id), ct);

                if (!result.Success)
                    return Results.NotFound(new { error = result.Error });

                return Results.Ok(result.Value);
            });

            // ---------------------------------------------------------
            // GET /api/drivers  (Paginated + multi-filter)
            // e.g. /api/drivers?page=1&pageSize=10&cin=AA1234
            // ---------------------------------------------------------
            group.MapGet("/", async (
                int page,
                int pageSize,
                string? cin,
                string? nom,
                string? numero,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var query = new GetDriversQuery(page, pageSize, cin, nom, numero);

                var result = await mediator.Send(query, ct);

                return result.Success
                    ? Results.Ok(result.Value)
                    : Results.BadRequest(new { error = result.Error });
            });

            group.MapPut("/{id:int}", async (int id, CreateDriverCommand body, IMediator mediator, CancellationToken ct) =>
            {
                var cmd = new UpdateDriverCommand(
                    id,
                    body.Cin,
                    body.Nom,
                    body.Prenom,
                    body.Numero,
                    body.Permis,
                    body.NomComplet);
                var result = await mediator.Send(cmd, ct);

                return result.Success
                    ? Results.Ok(new { success = true })
                    : Results.BadRequest(new { error = result.Error });
            });

            group.MapDelete("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(new DeleteDriverCommand(id), ct);

                return result.Success
                    ? Results.NoContent()
                    : Results.BadRequest(new { error = result.Error });
            });

            return app;
        }
    }
}
