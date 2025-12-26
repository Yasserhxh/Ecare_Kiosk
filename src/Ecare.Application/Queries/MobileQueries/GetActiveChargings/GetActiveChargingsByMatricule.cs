using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.MobileQueries.GetActiveChargings
{
    public sealed class FluxItemRow
    {
        public int Id { get; set; }
        public DateTime? StartChargingAt { get; set; }
        public string Matricule { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string BonDeCommande { get; set; } = default!;
        public string? Ligne { get; set; }
        public decimal Quantity { get; set; }
        public string Unite { get; set; } = default!;
        public string? Type { get; set; }
    }

    // DTOs
    public sealed record ChargingItemDto(
        string productName,
        decimal quantity,
        string unite,
        string? type);

    public sealed record ChargingGroupVm(
        string matricule,
        
        bool isTcharging,
        DateTime? startChargingAt,
        string? ligne,
        string bonDeCoammande,
        IReadOnlyList<int> fluxIds,
        IReadOnlyList<ChargingItemDto> items);

    // Query with optional filters
    public sealed record GetActiveChargingsByMatriculeQuery(string? Ligne = null, string? Type = null)
        : IRequest<Result<IReadOnlyDictionary<string, ChargingGroupVm>>>;
}
