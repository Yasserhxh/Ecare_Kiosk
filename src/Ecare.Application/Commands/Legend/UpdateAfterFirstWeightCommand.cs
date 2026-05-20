using Ecare.Shared;
using MediatR;
using System.Text.Json.Serialization;

namespace Ecare.Application.Commands.Legend;

public sealed record UpdateAfterFirstWeightCommand(
    [property: JsonConverter(typeof(FlexibleStringJsonConverter))]
    string RfidCard,
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
