using Ecare.Application.Queries;
using MediatR;

namespace Ecare.Api.Endpoints;

public static class KioskEndpoints
{
    public static IEndpointRouteBuilder MapKioskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/kiosk/scan", async (ScanBySlvQuery q, IMediator m) =>
            await m.Send(q));

        return app;
    }
}