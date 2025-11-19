using MediatR;

namespace Ecare.Application.Auth.Commands;

public record AssignRoleCommand(string UserId, string RoleName) : IRequest<bool>;
