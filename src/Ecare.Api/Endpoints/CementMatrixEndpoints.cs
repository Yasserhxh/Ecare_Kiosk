using Ecare.Application.Queries.GetCementLineMatrix;
using Ecare.Shared;
using MediatR;
using Microsoft.AspNetCore.Routing;

namespace Ecare.Api.Endpoints;

public static class CementMatrixEndpoints
{
    public static IEndpointRouteBuilder MapCementMatrixEndpoints(this IEndpointRouteBuilder app)

    {
        // Base route: /admin/ciments/matrix
        var group = app.MapGroup("/admin/ciments/matrix")
           .WithTags("Lines");

        // GET /admin/ciments/matrix?usine=ASMENT-TEMARA
        group.MapGet("/", async (string? usine, ISender sender, CancellationToken ct) =>
        {
            var query = new GetCementLineMatrixQuery(usine);
            var result = await sender.Send(query, ct);

            if (!result.Success)
                return Results.BadRequest(new { error = result.Error });

            return Results.Ok(result.Value);   // List<CementMatrixRowVm>
        });

        

        return app;
    }
}


