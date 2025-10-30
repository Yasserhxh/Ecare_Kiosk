using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Queries.Loading.UpdateStartLoading;

public sealed record UpdateStartChargingCommand(
    string Matricule,
    string BonDeCommande
) : IRequest<Result<int>>;
