using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetLegendById
{
    public sealed record GetLegendByIdQuery(int Id)
    : IRequest<Result<LegendFullVm>>;

    public sealed class LegendFullVm
    {
        public int Id { get; set; }
        public int? OrderId { get; set; }
        public int? CommercialOrderId { get; set; }

        public string? ClientName { get; set; }
        public string? Chantier { get; set; }
        public string? Matricule { get; set; }
        public string? RFIDCard { get; set; }

        public string? TypeCamion { get; set; }
        public string? ChauffeurName { get; set; }
        public string? CodeTransporteurSap { get; set; }
        public string? TransporteurName { get; set; }

        public int? NombrePlombs { get; set; }

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
        public int? ElapsedTimeParking { get; set; }

        public DateTime? PabEntryAt { get; set; }
        public DateTime? StartChargingAt { get; set; }
        public int? ElapsedInPab_Charging { get; set; }

        public DateTime? FinishedChargingAt { get; set; }
        public int? ElapsedCharging { get; set; }

        public DateTime? PabExitAt { get; set; }
        public int? ElapsedTimeInF_Exit { get; set; }

        public int? TotalTimeInCercuit { get; set; }

        public string? BonDeLivraison { get; set; }
        public int? Step { get; set; }

        public bool? IsPined { get; set; }
        public DateTime? PinedAt { get; set; }

        public DateTime? AddedToQueueAt { get; set; }
        public DateTime? FirstPlaceAt { get; set; }
        public int? TimeElapsedInFirstPlace { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? DateAffectation { get; set; }

        public string? BonDeCommande { get; set; }
        public string? Ligne { get; set; }

        public string? ChequeImg { get; set; }

        public int? ExtraSac { get; set; }
        public int? PlusBags { get; set; }
        public int? MinusBags { get; set; }

        public DateTime? StartExtraSac { get; set; }
        public DateTime? EndExtraSac { get; set; }
        public int? ElapsedExtraSac { get; set; }

        public string? UserId { get; set; }

        public string? CodeSapChantier { get; set; }
        public string? CodeSapClient { get; set; }
        public string? CodeSapCommande { get; set; }
        public string? CodeSapProduit1 { get; set; }
        public string? CodeSapProduit2 { get; set; }

        public int? SacNumber { get; set; }
        public int? NumberSacs_Charged { get; set; }
        public decimal? Weight_Charged { get; set; }

        public string? Status { get; set; }
        public string? LoadingStatus { get; set; }

        public string? PlombNumber { get; set; }
        public string? Plombs { get; set; }

        public string? SecondLigne { get; set; }
        public string? PermisDeConduite { get; set; }

        public decimal? PTAC { get; set; }
        public decimal? TARE { get; set; }

        public int? AnnulationCommercial { get; set; }
        public string? MotifAnnulationCommercial { get; set; }
        public string? UserIdAnnulationCommercial { get; set; }
        public bool IsLowCreditDeliveryRisk { get; set; }
    }
}
