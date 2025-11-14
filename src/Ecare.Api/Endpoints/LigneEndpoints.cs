using Ecare.Application.Queries.AffectTruckToLigne;
using MediatR;

namespace Ecare.Api.Endpoints
{
    public static class LigneEndpoints
    {
        public static IEndpointRouteBuilder MapLigneEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/api/ligne/affect", 
                async (
                AffectTruckToLigneQuery body,
                ISender mediator,
                CancellationToken ct) =>
                {
                    var result = await mediator.Send(new AffectTruckToLigneQuery(body.Produit,body.Matricule,body.BonDeCommande), ct);
                    return Results.Ok(new { message = result });
                }
            )
            .WithName("AffectTruckToLigne")
            .Produces(StatusCodes.Status200OK);



            return app;
        }
    }
}
