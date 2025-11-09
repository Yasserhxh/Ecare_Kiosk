using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Flux;

public sealed record UpdateFirstWeightByBonCommand(
    string Matricule,
    string BonDeCommande,
    decimal FirstWeight
) : IRequest<Result<int>>;