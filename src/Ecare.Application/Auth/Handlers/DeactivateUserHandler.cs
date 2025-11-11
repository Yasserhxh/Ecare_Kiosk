using Ecare.Application.Auth.Commands;
using Ecare.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Ecare.Application.Auth.Handlers;

public class DeactivateUserHandler(
    UserManager<ApplicationUser> userManager) : IRequestHandler<DeactivateUserCommand, bool>
{
    public async Task<bool> Handle(DeactivateUserCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString())
                   ?? throw new Exception("User not found");

        user.IsActive = false;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded;
    }
}
