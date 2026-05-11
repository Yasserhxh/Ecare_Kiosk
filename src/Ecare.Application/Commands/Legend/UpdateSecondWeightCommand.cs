using Ecare.Application.Dtos;
using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Legend;

public sealed record UpdateSecondWeightCommand(
    int RfidCard,
    string Matricule,
    int DeuxiemePoid,
    int? LegendId = null
) : IRequest<Result<UpdateSecondWeightResult>>;

public sealed class UpdateSecondWeightResult
{
    public bool Success { get; init; }
    public BonDeLivraisonDto? BonDeLivraison { get; init; }
}
