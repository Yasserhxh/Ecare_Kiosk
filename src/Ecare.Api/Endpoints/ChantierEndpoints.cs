using Ecare.Application.Commands.AffectChantierToPartner;
using Ecare.Application.Commands.CreateChantier;
using Ecare.Application.Queries.ChantierDropdown;
using Ecare.Application.Queries.GetChantiers;
using MediatR;

namespace Ecare.Api.Endpoints;

public static class ChantierEndpoints
{
    public static IEndpointRouteBuilder MapChantierEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chantiers")
                       .WithTags("Chantier");

        // ----------------------------------------------------
        // GET PAGINATED CHANTIERS
        // ----------------------------------------------------
        group.MapGet("/", async (
            int page,
            int pageSize,
            string? code,
            string? name,
            int? partnerId,
            IMediator med,
            CancellationToken ct) =>
        {
            var query = new GetChantiersQuery(page, pageSize, code, name, partnerId);
            var result = await med.Send(query, ct);

            return result.Success
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        });

        // ----------------------------------------------------
        // ASSIGN CHANTIER TO PARTNER
        // ----------------------------------------------------
        group.MapPost("/assign-partner", async (
            AffectChantierToPartnerCommand cmd,
            IMediator med,
            CancellationToken ct) =>
        {
            var result = await med.Send(cmd, ct);

            return result.Success
                ? Results.Ok(new { message = result.Value })
                : Results.BadRequest(new { error = result.Error });
        });

        group.MapGet("/dropdown", async (
        IMediator med,
        CancellationToken ct) =>
        {
            var result = await med.Send(new GetChantiersDropdownQuery(), ct);

            return result.Success
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        });

        group.MapPost("/", async (
        CreateChantierCommand cmd,
        IMediator med,
        CancellationToken ct) =>
        {
            var result = await med.Send(cmd, ct);

            return result.Success
                ? Results.Created($"/api/chantiers/{result.Value}", new { id = result.Value })
                : Results.BadRequest(new { error = result.Error });
        });

        return app;
    }
}
