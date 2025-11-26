using Ecare.Application.Commands;
using Ecare.Application.Commands.CreateLegacyOrderLegend;
using Ecare.Application.Commands.Orders;
using Ecare.Application.Commands.Orders.UpdateOrder;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Ecare.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders/confirm", async (ConfirmOrderCommand c, IMediator m) =>
            await m.Send(c));

        app.MapPost("/orders/cancel", async (CancelOrderCommand c, IMediator m) =>
            await m.Send(c));

        app.MapPost("/orders", async (CreateOrderAtKioskCommand c, IMediator m, CancellationToken ct) =>
            await m.Send(c, ct));

        app.MapPost("/orders/legacy", async (CreateLegacyOrderCommand c, IMediator m, CancellationToken ct) =>
            await m.Send(c, ct));

        app.MapPost("/orders/from-form", async (CreateOrderFromFormCommand c, IMediator m, CancellationToken ct) =>
            await m.Send(c, ct));

        app.MapPut("/{orderId:int}/cheque-image",
            async (
                int orderId,
                [FromBody] UpdateOrderCommand request,
                ISender mediator,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.ImageName))
                    return Results.BadRequest("ImageName is required.");

                var success = await mediator.Send(
                    new UpdateOrderCommand(orderId, request.ImageName),
                    ct);

                return success
                    ? Results.NoContent()
                    : Results.NotFound($"Order {orderId} not found.");
            })
            .WithName("UpdateOrderChequeImage")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);


        //New Confirm Endpoint 
        app.MapPost("/legend-orders", async (
        CreateLegacyOrderLegendCommand cmd,
        IMediator mediator) =>
            {
                var result = await mediator.Send(cmd);

                if (!result.Success)
                    return Results.BadRequest(result.Error);

                return Results.Ok(new { id = result.Value });
            });


        return app;
    }
}