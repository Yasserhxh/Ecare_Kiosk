using MediatR;

namespace Ecare.Application.Auth.Commands;

public record DeactivateUserCommand(string UserId) : IRequest<bool>;
