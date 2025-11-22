using Ecare.Application.Queries.CimentDropdown;
using MediatR;

namespace Ecare.Api.Endpoints
{
    public static class ProduitEndpoints
    {
        public static IEndpointRouteBuilder MapProduitEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/produits")
                           .WithTags("Produit");

            // ----------------------------------------------------
            // GET PRODUIT DROPDOWN
            // ----------------------------------------------------
            group.MapGet("/dropdown", async (
                IMediator med,
                CancellationToken ct) =>
            {
                var result = await med.Send(new GetCimentsDropdownQuery(), ct);

                return result.Success
                    ? Results.Ok(result.Value)
                    : Results.BadRequest(new { error = result.Error });
            });

            return app;
        }
    }
}
