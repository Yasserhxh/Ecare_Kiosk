using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Legend;

public sealed record UpdateAfterFirstWeightCommand(
    int RfidCard,
    string Matricule,
    int PremierePoid,
    string Produit1,
    int? LegendId = null
) : IRequest<Result<FirstWeightResultVm>>;


public sealed class FirstWeightResultVm
{
    public int LigneId { get; set; }
    public string LigneName { get; set; } = string.Empty;
    public string? LigneImageUrl { get; set; }
}
