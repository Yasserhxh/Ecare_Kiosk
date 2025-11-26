using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetFluxStagesSummary
{
    public sealed record FluxStageItem(
    string Matricule,
    string? ClientName,
    string? Ligne,
    string? Type,
    DateTime? ReferenceTime,
    double MinutesInStage);

    public sealed record FluxStageSummary(
        string Stage,
        int TruckCount,
        double MinMinutes,
        double MaxMinutes,
        double AvgMinutes,
        IReadOnlyList<FluxStageItem> Trucks);

    public sealed record GetFluxStagesSummaryQuery(string? Type = null)
        : IRequest<Result<IReadOnlyList<FluxStageSummary>>>;
}
