using Ecare.Application.Auth.Responses;
using MediatR;

namespace Ecare.Application.Auth.Queries;

public record LoginQuery(string Identifier, string Password) : IRequest<AuthResponse>;

