using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Queries.MobileQueries.GetFluxChargingDetails
{
    public sealed record FluxStageItem(
        string Matricule,
        string? ClientName,
        string? Ligne,
        string? Type,
        double MinutesInStage);

    public sealed record FluxStageSummary(
        string Stage,
        int TruckCount,
        double MinMinutes,
        double MaxMinutes,
        double AvgMinutes,
        IReadOnlyList<FluxStageItem> Trucks);

    // Type = VRAC / SAC / PAL ... (optional)
    // DateFrom / DateTo = date filters on ParkedAt (optional)
    public sealed record GetFluxChargingDetailsQuerie(
        string? Type = null,
        DateTime? DateFrom = null,
        DateTime? DateTo = null)
        : IRequest<Result<IReadOnlyList<FluxStageSummary>>>;
}

