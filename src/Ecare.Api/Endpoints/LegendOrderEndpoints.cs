using Ecare.Application.Commands.CreateLegacyOrderLegend;
using Ecare.Application.Commands.GeneratePlombs;
using Ecare.Application.Commands.Legend;
using Ecare.Application.Commands.LegendExtraSac;
using Ecare.Application.Commands.UpdateOrderLegend;
using MediatR;

namespace Ecare.Api.Endpoints
{
    public static class LegendOrderEndpoints
    {
        public static IEndpointRouteBuilder MapLegendOrderEndpoints(this IEndpointRouteBuilder app) {

            var group = app.MapGroup("/legend")
                .WithTags("Order Legend");

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

                if (!result.Success)
                    return Results.BadRequest(new { success = false, error = result.Error });

                return Results.Ok(new
                {
                    success = true,
                    updatedOrderId = result.Value?.UpdatedOrderId
                });
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


            return app;
        }
    }
}
