using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.MobileQueries.GetFluxChargingDetails
{
    public sealed class FluxChargingRow
    {
        public string? CarteSlv { get; set; }
        public decimal? FirstWeight { get; set; }
        public string Matricule { get; set; } = default!;
        public string? ClientName { get; set; }
        public string ProductName { get; set; } = default!;
        public decimal Quantity { get; set; }
        public string? ProductType { get; set; }
        public DateTime? StartChargingAt { get; set; }
        public DateTime? FinishedChargingAt { get; set; }
        public string? Ligne { get; set; }
    }

    public sealed record FluxChargingItemDto(
        string productName,
        decimal quantity,
        string? productType);

    public sealed record FluxChargingGroupVm(
        string matricule,
        string? carteSlv,
        string? clientName,
        decimal? firstWeight,
        string? ligne,
        bool isTcharging,
        bool isFinished,
        DateTime? startChargingAt,
        DateTime? finishedChargingAt,
        IReadOnlyList<FluxChargingItemDto> items);

    public sealed record GetFluxChargingDetailsByIdQuery(int EfId)
        : IRequest<Result<FluxChargingGroupVm?>>;
}
