using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.EcareDriver;

public sealed record DeleteDriverCommand(int Id) : IRequest<Result<bool>>;
