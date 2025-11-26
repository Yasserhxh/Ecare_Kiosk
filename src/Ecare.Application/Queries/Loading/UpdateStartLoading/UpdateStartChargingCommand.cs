using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Flux;

public sealed record UpdateStartChargingCommand(
    string Matricule,
    string BonDeCommande
) : IRequest<Result<int>>;
