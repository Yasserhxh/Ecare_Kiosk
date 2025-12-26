using MediatR;

namespace Ecare.Application.Queries.Legend;

public sealed class GetLegendBusinessQuery
    : IRequest<IReadOnlyList<LegendBusinessRow>>
{
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }

    public int? HourFrom { get; init; }
    public int? HourTo { get; init; }

    // Default Step < 5
    public int MaxStep { get; init; } = 5;

    // DB column filters (Matricule, Chantier, Produit1, etc.)
    public Dictionary<string, object>? Filters { get; init; }
}
