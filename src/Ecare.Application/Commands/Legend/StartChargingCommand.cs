using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Legend;

public sealed record StartChargingCommand(
    string RfidCard,
    string Matricule,
    int? LegendId = null,
    int? LoadingPointTare = null
) : IRequest<Result<bool>>;
