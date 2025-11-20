using Ecare.Application.Commands.CreateEcareTruck;
using Ecare.Application.Commands.CreateEcareTruckType;
using Ecare.Application.Queries.GetTruck;
using Ecare.Application.Queries.GetTruckTypes;
using MediatR;
using Microsoft.AspNetCore.Routing;

namespace Ecare.Api.Endpoints;

public static class TruckEndpoints
{
    public static IEndpointRouteBuilder MapTruckEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/trucks")
            .WithTags("Truck-TruckType");

        // ---------------------------------------------------------
        // 🚚 TRUCK TYPE ENDPOINTS
        // ---------------------------------------------------------

        // CREATE TRUCK TYPE
        group.MapPost("/types", async (
            CreateTruckTypeCommand cmd,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(cmd, ct);
            return result.Success
                ? Results.Created($"/api/trucks/types/{result.Value}", new { id = result.Value })
                : Results.BadRequest(new { error = result.Error });
        });

        // GET PAGINATED TRUCK TYPES
        group.MapGet("/types", async (
            int page,
            int pageSize,
            string? type,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetTruckTypesQuery(page, pageSize, type);
            var result = await mediator.Send(query, ct);
            return result.Success
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        });

        // GET TRUCK TYPE DROPDOWN
        group.MapGet("/types/dropdown", async (
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetTruckTypesDropdownQuery(), ct);
            return result.Success
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        });


        // ---------------------------------------------------------
        // TRUCK ENDPOINTS
        // ---------------------------------------------------------

        // CREATE TRUCK
        group.MapPost("/", async (
            CreateTruckCommand cmd,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(cmd, ct);
            return result.Success
                ? Results.Created($"/api/trucks/{result.Value}", new { id = result.Value })
                : Results.BadRequest(new { error = result.Error });
        });

        // GET PAGINATED TRUCKS WITH FILTERS + JOIN
        group.MapGet("/", async (
            int page,
            int pageSize,
            string? matricule,
            int? truckTypeId,
            int? driverId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetTrucksQuery(page, pageSize, matricule, truckTypeId, driverId);
            var result = await mediator.Send(query, ct);

            return result.Success
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        });

        // GET TRUCK DROPDOWN (Id + Matricule)
        group.MapGet("/dropdown", async (
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetTrucksDropdownQuery(), ct);
            return result.Success
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        });

        return app;
    }
}
