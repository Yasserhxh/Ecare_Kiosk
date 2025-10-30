using Ecare.Application.Commands.CancelOrder;
using Ecare.Application.Commands.ConfirmOrder;
using Ecare.Application.Commands.CreateKioskOrder;
using Ecare.Application.Commands.CreateLegacyOrder;
using Ecare.Application.Commands.Orders.AffectOrder;
using MediatR;

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

        return app;
    }
}