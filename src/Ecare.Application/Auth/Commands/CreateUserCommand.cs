using MediatR;

namespace Ecare.Application.Auth.Commands;

public record CreateUserCommand(
    string UserName,
    string Nom,
    string Prenom,
    string Email,
    string Password) : IRequest<string>;
