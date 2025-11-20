using Ecare.Application.Commands.AffectTruckToPartner;
using Ecare.Application.Commands.UnlinkPartnerFromTruck;
using Ecare.Application.Queries.PartnerTruck;
using MediatR;

namespace Ecare.Api.Endpoints;

public static class PartnerTruckEndpoints
{
    public static IEndpointRouteBuilder MapPartnerTruckEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/partners")
                       .WithTags("Partner-Truck");

        // ----------------------------------------------------
        // ASSIGN TRUCK TO PARTNER
        // POST /api/partners/assign-truck
        // ----------------------------------------------------
        group.MapPost("/assign-truck", async (
            AffectTruckToPartnerCommand cmd,
            IMediator med,
            CancellationToken ct) =>
        {
            var result = await med.Send(cmd, ct);

            return result.Success
                ? Results.Ok(new { message = result.Value })
                : Results.BadRequest(new { error = result.Error });
        });

        // ----------------------------------------------------
        // UNLINK PARTNER FROM TRUCK
        // DELETE /api/partners/unlink-truck/{partnerId}
        // ----------------------------------------------------
        group.MapDelete("/unlink-truck/{partnerId:int}", async (
            int partnerId,
            IMediator med,
            CancellationToken ct) =>
        {
            var result = await med.Send(
                new UnlinkPartnerFromTruckCommand(partnerId),
                ct
            );

            return result.Success
                ? Results.Ok(new { message = result.Value })
                : Results.BadRequest(new { error = result.Error });
        });

        group.MapGet("/truck-links", async (
           int page,
           int pageSize,
           IMediator med,
           CancellationToken ct) =>
        {
            var query = new GetPartnerTrucksQuery(page, pageSize);
            var result = await med.Send(query, ct);

            return result.Success
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        });

        return app;
    }
}
