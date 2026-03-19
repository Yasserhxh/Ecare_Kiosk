using Dapper;
using Ecare.Application.Commands.CancelLegend;
using Ecare.Application.Commands.CreateLegacyOrderLegend;
using Ecare.Application.Commands.GeneratePlombs;
using Ecare.Application.Commands.Legend;
using Ecare.Application.Commands.LegendExtraSac;
using Ecare.Application.Commands.MergeParkingWithSap;
using Ecare.Application.Commands.ProcessParking;
using Ecare.Application.Commands.UpdateCircuit.DeleteFirstPesage;
using Ecare.Application.Commands.UpdateCircuit.DeleteSecondPesage;
using Ecare.Application.Commands.UpdateOrderLegend;
using Ecare.Application.Queries.GetLegendsDocument;
using Ecare.Application.Queries.GetOrderBySapCode;
using MediatR;
using System.Data;

namespace Ecare.Api.Endpoints
{
    public static class LegendOrderEndpoints
    {
        public static IEndpointRouteBuilder MapLegendOrderEndpoints(this IEndpointRouteBuilder app) {

            var group = app.MapGroup("/legend")
                .WithTags("Order Legend");

            group.MapPost("/parking/process", async (
               ProcessParkingCommand cmd,
               IMediator mediator,
               CancellationToken ct) =>
            {
                var result = await mediator.Send(cmd, ct);

                if (!result.Success)
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = result.Error
                    });

                return Results.Ok(new
                {
                    success = true,
                    value = result.Value
                });
            });


            group.MapPost("/legend-orders", async (
               CreateLegacyOrderLegendCommand cmd,
               IMediator mediator) =>
            {
                var result = await mediator.Send(cmd);

                if (!result.Success)
                    return Results.BadRequest(result.Error);

                return Results.Ok(new { id = result.Value });
            });

            group.MapPost("/legend/first-weight", async (
            UpdateAfterFirstWeightCommand cmd,
            IMediator mediator) =>
            {
                var result = await mediator.Send(cmd);

                if (!result.Success)
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = result.Error
                    });

                return Results.Ok(new
                {
                    success = true,
                    ligne = new
                    {
                        id = result.Value!.LigneId,
                        name = result.Value.LigneName,
                        imageUrl = result.Value.LigneImageUrl
                    }
                });
            });

            group.MapPost("/legend/start-charging", async (
            StartChargingCommand cmd, IMediator mediator) =>
            {
                var result = await mediator.Send(cmd);
                return result.Success ? Results.Ok(new { success = true })
                                      : Results.BadRequest(result.Error);
            });

            group.MapPost("/legend/finish-charging", async (
                FinishChargingCommand cmd, IMediator mediator) =>
            {
                var result = await mediator.Send(cmd);
                return result.Success ? Results.Ok(new { success = true })
                                      : Results.BadRequest(result.Error);
            });

            group.MapPost("/legend/second-weight", async (
                UpdateSecondWeightCommand cmd,
                IMediator mediator) =>
            {
                var result = await mediator.Send(cmd);

                 

                return result;
            });

            group.MapPost("/legend/extrasac", async (
            UpdateExtraSacCommand cmd,
            IMediator mediator) =>
            {
                var result = await mediator.Send(cmd);

                if (!result.Success)
                    return Results.BadRequest(result.Error);

                return Results.Ok(new { success = true });
            });


            app.MapPost("/plombs/generate", async (
            GeneratePlombsCommand cmd,
            IMediator mediator) =>
            {
                var result = await mediator.Send(cmd);
                return result.Success
                    ? Results.Ok(result.Value)
                    : Results.BadRequest(result.Error);
            });


            group.MapPut("/legend", async (
            UpdateOrderLegendCommand cmd,
            IMediator med,
            CancellationToken ct) =>
            {
                var result = await med.Send(cmd, ct);

                return result.Success
                    ? Results.Ok(new { message = result.Value })
                    : Results.BadRequest(new { error = result.Error });
            });

            group.MapGet("/orders/by-sap/{codeSapCommande}", async (
            string codeSapCommande,
            IMediator mediator) =>
            {
                var result = await mediator.Send(new GetOrderBySapCodeQuery(codeSapCommande));

                return result.Success
                    ? Results.Ok(result.Value)
                    : Results.NotFound(result.Error);
            });

            group.MapPost("/merge/order", async (MergeParkingRequest req, IMediator mediator) =>
            {
                var result = await mediator.Send(
                    new MergeParkingWithSapCommand(req.Matricule, req.CodeSapCommande));

                return result.Success
                    ? Results.Ok(new { updated = result.Value })
                    : Results.BadRequest(result.Error);
            });


            group.MapPost("/legend/{id}/cancel", async (
                int id,
                IMediator mediator,
                CancellationToken ct) =>
            {
                bool ok = await mediator.Send(new CancelLegendCommand(id), ct);

                return ok
                    ? Results.Ok(new { message = "Legend canceled successfully." })
                    : Results.NotFound(new { message = "Legend not found." });
            });

            group.MapGet("/legends-documents", async (
                string? clientName,
                string? matricule,
                string? produit1,
                int page,
                int pageSize,
                IMediator mediator,
                CancellationToken ct
            ) =>
            {
                var result = await mediator.Send(new GetLegendsQuery(
                    clientName,
                    matricule,
                    produit1,
                    page <= 0 ? 1 : page,
                    pageSize <= 0 ? 20 : pageSize
                ), ct);

                return Results.Ok(result);
            });


            group.MapPost("/cancel-first-pesage", async (int id,string user, IMediator mediator) =>
            {
                var result = await mediator.Send(
                    new DeleteFirstPesageCommand { Id = id,FirstPesageCanceledBy=user });

                return result;
            });

            group.MapPost("/cancel-second-pesage", async (int id, string user, IMediator mediator) =>
            {
                var result = await mediator.Send(
                    new DeleteSecondPesageCommand { Id = id, SecondPesageCanceledBy = user });

                return result;
            });




            return app;
        }

        public sealed class MergeParkingRequest
        {
            public string Matricule { get; set; } = "";
            public string CodeSapCommande { get; set; } = "";
        }
    }

    
}
