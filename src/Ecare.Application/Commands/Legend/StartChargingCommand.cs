using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Legend;

public sealed record StartChargingCommand(
    int RfidCard,
    string Matricule
) : IRequest<Result<bool>>;
