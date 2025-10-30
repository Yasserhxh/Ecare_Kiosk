using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Queue.UpdateSecondWeight;

public sealed record UpdateSecondWeightByBonCommand(
    string Matricule,
    string BonDeCommande,
    decimal SecondWeight
) : IRequest<Result<int>>;