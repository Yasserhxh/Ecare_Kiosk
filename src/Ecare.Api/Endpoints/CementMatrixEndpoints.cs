using Ecare.Application.Commands.AffectCimentToLigne;
using Ecare.Application.Commands.ChangeLigneCapacity;
using Ecare.Application.Commands.ToggleLigneCiment;
using Ecare.Application.Commands.ToggleLigneStatus;
using Ecare.Application.Queries.GetCementLineMatrix;
using Ecare.Application.Queries.GetCementLinesFlat;
using Ecare.Application.Queries.GetOrderCheque;
using Ecare.Shared;
using MediatR;
using Microsoft.AspNetCore.Routing;

namespace Ecare.Api.Endpoints;

public static class CementMatrixEndpoints
{
    public sealed record AffectCimentToLigneRequest(int LigneId, int CimentId);
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

            return Results.Ok(result.Value);   // CementMatrixVm
        });

        group.MapPost("/affect", async (
            AffectCimentToLigneRequest dto,
            ISender sender,
            CancellationToken ct) =>
        {
            var cmd = new AffectCimentToLigneCommand(dto.LigneId, dto.CimentId);
            var result = await sender.Send(cmd, ct);

            if (!result.Success)
            {
                // All business messages already in français in Result.Fail(...)
                return Results.BadRequest(new
                {
                    success = false,
                    message = result.Error
                });
            }

            return Results.Ok(new
            {
                success = true,
                message = "Produit affecté à la ligne avec succès."
            });
        })
        .WithName("AffectCimentToLigne");

        group.MapGet("/lignes-status", async (string? usine, ISender sender, CancellationToken ct) =>
        {
            var query = new GetCementLinesFlatQuery(usine);
            var result = await sender.Send(query, ct);

            if (!result.Success)
                return Results.BadRequest(new { error = result.Error });

            return Results.Ok(result.Value);
        })
        .WithName("GetCementLinesFlat");

        group.MapPost("/{ligneId:int}/toggle-status", async (
            int ligneId,
            ISender sender,
            CancellationToken ct) =>
        {
            var cmd = new ToggleLigneStatusCommand(ligneId);
            var result = await sender.Send(cmd, ct);

            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = result.Error
                });
            }

            return Results.Ok(new
            {
                success = true,
                ligneId,
                newStatus = result.Value // 0 ou 1
            });
        })
        .WithName("ToggleLigneStatus");

        group.MapPost("/{ligneId:int}/capacity", async (
           int ligneId,
           ChangeCapacityRequest body,
           ISender sender,
           CancellationToken ct) =>
        {
            var cmd = new ChangeLigneCapacityCommand(ligneId, body.Capacity);
            var result = await sender.Send(cmd, ct);

            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = result.Error
                });
            }

            return Results.Ok(new
            {
                success = true,
                ligneId,
                capacity = result.Value
            });
        })
       .WithName("ChangeLigneCapacity");

        group.MapGet("/cheques", async (
            int page,
            int pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetOrderChequesPagedQuery(page, pageSize);
            var result = await sender.Send(query, ct);

            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = result.Error
                });
            }

            return Results.Ok(new
            {
                success = true,
                data = result.Value // { items, page, pageSize, totalCount }
            });
        })
        .WithName("GetOrderChequesPaged");

        app.MapPut("/ligne/{ligneId}/ciment/{cimentId}/toggle",
        async (int ligneId, int cimentId, IMediator mediator) =>
        {
            var result = await mediator.Send(
                new ToggleLigneCimentCommand(ligneId, cimentId));

            return result.Success
                ? Results.Ok(new
                {
                    LigneId = ligneId,
                    CimentId = cimentId,
                    NewActif = result.Value
                })
                : Results.BadRequest(result.Error);
        })
        .WithName("ToggleLigneCiment")
        .WithTags("Lignes")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);


        return app;
    }

    public sealed record ChangeCapacityRequest(int Capacity);
}


