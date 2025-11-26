using Ecare.Application.Auth.Commands;
using Ecare.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Ecare.Application.Auth.Handlers;

public class ActivateUserHandler(
    UserManager<ApplicationUser> userManager) : IRequestHandler<ActivateUserCommand, bool>
{
    public async Task<bool> Handle(ActivateUserCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
            throw new Exception("User not found");

        if (user.IsActive)
            return true; 

        user.IsActive = true;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded;
    }
}
