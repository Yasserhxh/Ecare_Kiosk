using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Legend;

public sealed record FinishChargingCommand(
    string DeviceName,
    string RfidCard,
    string Matricule,
    int NumberSacs_Charged,
    int Weight_Charged,
    int? LegendId = null
) : IRequest<Result<bool>>;
