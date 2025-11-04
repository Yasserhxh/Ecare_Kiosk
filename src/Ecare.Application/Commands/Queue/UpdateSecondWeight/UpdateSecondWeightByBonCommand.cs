using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Flux;

public sealed record UpdateSecondWeightByBonCommand(
    string Matricule,
    string BonDeCommande,
    decimal SecondWeight,
    decimal FirstWeight
) : IRequest<Result<int>>;