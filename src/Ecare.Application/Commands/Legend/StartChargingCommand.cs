using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Legend;

public sealed record StartChargingCommand(
    string RfidCard,
    string Matricule
) : IRequest<Result<bool>>;
