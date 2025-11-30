using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetLegendsDocument
{
    public sealed record GetLegendsQuery(
    string? ClientName,
    string? Matricule,
    string? Produit1,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedLegendResult>;

    public sealed class LegendDto
    {
        public int Id { get; set; }
        public string? ClientName { get; set; }
        public string? Chantier { get; set; }
        public string? Matricule { get; set; }
        public string? RFIDCard { get; set; }
        public string? Produit1 { get; set; }
        public decimal? Quantite1 { get; set; }
        public string? Produit2 { get; set; }
        public decimal? Quantite2 { get; set; }
        public string? TypeProduit { get; set; }
        public string? CodeClientSAP { get; set; }
        public string? CodeProduitSAP { get; set; }
        public decimal? PremierePoid { get; set; }
        public decimal? DeuxiemePoid { get; set; }
        public DateTime? ParkingAt { get; set; }
        public DateTime? PabEntryAt { get; set; }
        public DateTime? StartChargingAt { get; set; }
        public DateTime? FinishedChargingAt { get; set; }
        public DateTime? PabExitAt { get; set; }
        public string? BonDeLivraison { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? BonDeCommande { get; set; }
        public string? Ligne { get; set; }
        public int? ExtraSac { get; set; }
        public int? PlusBags { get; set; }
        public int? MinusBags { get; set; }
        public DateTime? StartExtraSac { get; set; }
        public DateTime? EndExtraSac { get; set; }
        public int? SacNumber { get; set; }
        public int? NumberSacs_Charged { get; set; }
        public decimal? Weight_Charged { get; set; }
        public string? Status { get; set; }
    }

    public sealed class PagedLegendResult
    {
        public IEnumerable<LegendDto> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
