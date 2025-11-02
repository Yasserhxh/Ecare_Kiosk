using MediatR;
using Microsoft.Data.SqlClient;

namespace Ecare.Api.Endpoints
{
    public static class DeviceEndpoints
    {
        public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app) {

            app.MapPost("/devices/hello", async (HelloDeviceCommand cmd, IMediator m) =>
            {
                var res = await m.Send(cmd);
                return Results.Ok(res);
            });

            app.MapPost("/devices/assign", async (AssignDeviceToLineCommand cmd, IMediator m) =>
            {
                try
                {
                    var res = await m.Send(cmd);
                    return Results.Ok(res);
                }
                catch (SqlException ex) when (ex.Number == 50001) // line already has device
                {
                    return Results.Conflict(new { error = ex.Message });
                }
            });

            app.MapGet("/devices/{deviceId}/assignment", async (string deviceId, IMediator m) =>
            {
                var res = await m.Send(new GetDeviceAssignmentQuery(deviceId));
                return Results.Ok(res);
            });

            app.MapGet("/", () => Results.Json(new
            {
                message = "Device Assignment API",
                endpoints = new[] {
                    "POST /devices/hello",
                    "POST /devices/assign",
                    "GET  /devices/{deviceId}/assignment"
                }
            }));

            return app;
        }
    }
}
