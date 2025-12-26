using Ecare.Application.Auth.Commands;
using Ecare.Application.Auth.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace Ecare.Api.Endpoints
{
    public static class AuthEndpoints
    {
        public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/auth/login", async (LoginQuery query, IMediator mediator)
                => Results.Ok(await mediator.Send(query)));

            app.MapPost("/auth/create", async (CreateUserCommand cmd, IMediator mediator)
                => Results.Ok(await mediator.Send(cmd)));

            app.MapPost("/auth/assign-role",  async (AssignRoleCommand cmd, IMediator mediator)
                => Results.Ok(await mediator.Send(cmd)));

            app.MapPost("/auth/deactivate", [Authorize(Roles = "Admin")] async (DeactivateUserCommand cmd, IMediator mediator)
                => Results.Ok(await mediator.Send(cmd)));

            app.MapPost("/auth/activate", async (ActivateUserCommand cmd, IMediator mediator) =>
            {
                var result = await mediator.Send(cmd);
                return result ? Results.Ok("User activated successfully")
                              : Results.BadRequest("Failed to activate user");
            });

            app.MapGet("/auth/users", [Authorize(Roles = "Admin")] async ([AsParameters] GetAllUsersQuery query, IMediator mediator) =>
            {
                var result = await mediator.Send(query);
                return Results.Ok(result);
            }).WithName("GetAllUsers")
            .WithTags("Auth"); ;

            app.MapGet("/auth/me", [Authorize] (HttpContext ctx) =>
            {
                var claims = ctx.User.Claims.Select(c => new { c.Type, c.Value }).ToList();
                return Results.Ok(claims);
            }).WithTags("Auth");

            return app;
        }
    }
}
