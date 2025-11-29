using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Legend;

public sealed record FinishChargingCommand(
    int RfidCard,
    string Matricule,
    int NumberSacs_Charged,
    int Weight_Charged
) : IRequest<Result<bool>>;
