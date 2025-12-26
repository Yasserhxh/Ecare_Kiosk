using Ecare.Application.Auth.Commands;
using Ecare.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Ecare.Application.Auth.Handlers;

public class AssignRoleHandler(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager) : IRequestHandler<AssignRoleCommand, bool>
{
    public async Task<bool> Handle(AssignRoleCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString())
                   ?? throw new Exception("User not found");

        if (!await roleManager.RoleExistsAsync(request.RoleName))
            await roleManager.CreateAsync(new ApplicationRole { Name = request.RoleName });

        var result = await userManager.AddToRoleAsync(user, request.RoleName);
        return result.Succeeded;
    }
}
