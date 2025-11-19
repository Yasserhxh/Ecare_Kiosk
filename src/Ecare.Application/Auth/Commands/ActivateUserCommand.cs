using MediatR;

namespace Ecare.Application.Auth.Commands;

public record ActivateUserCommand(string UserId) : IRequest<bool>;
