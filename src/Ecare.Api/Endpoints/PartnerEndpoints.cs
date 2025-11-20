using Ecare.Application.Commands.Partner;
using Ecare.Application.Queries.GetPartner;
using MediatR;

namespace Ecare.Api.Endpoints
{
    public static class PartnerEndpoints
    {
        public static IEndpointRouteBuilder MapPartnerEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/partners")
                           .WithTags("Partner");

            // -------------------------------------------------------
            // CREATE PARTNER
            // -------------------------------------------------------
            group.MapPost("/", async (
                CreatePartnerCommand cmd,
                IMediator med,
                CancellationToken ct) =>
            {
                var result = await med.Send(cmd, ct);

                return result.Success
                    ? Results.Created($"/api/partners/{result.Value}", new { id = result.Value })
                    : Results.BadRequest(new { error = result.Error });
            });

            // -------------------------------------------------------
            // GET PAGINATED PARTNERS
            // -------------------------------------------------------
            group.MapGet("/", async (
                int page,
                int pageSize,
                string? code,
                string? name,
                string? partnerType,
                IMediator med,
                CancellationToken ct) =>
            {
                var query = new GetPartnersQuery(page, pageSize, code, name, partnerType);
                var result = await med.Send(query, ct);

                return result.Success
                    ? Results.Ok(result.Value)
                    : Results.BadRequest(new { error = result.Error });
            });

            return app;
        }
    }
}
