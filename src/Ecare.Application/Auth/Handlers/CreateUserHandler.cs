using Ecare.Application.Auth.Commands;
using Ecare.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Ecare.Application.Auth.Handlers;

public class CreateUserHandler(
    UserManager<ApplicationUser> userManager) : IRequestHandler<CreateUserCommand, string>
{
    public async Task<string> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            Id =  Guid.NewGuid().ToString(),
            UserName = request.UserName,
            Nom = request.Nom,
            Prenom = request.Prenom,
            Email = request.Email,
            IsActive = true,
            RaisonSociale = "ASMENT-TEMARA"
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));

        return user.Id;
    }
}
