using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Queue.UpdateFirstWeight;

public sealed record UpdateFirstWeightByBonCommand(
    string Matricule,
    string BonDeCommande,
    decimal FirstWeight
) : IRequest<Result<int>>;