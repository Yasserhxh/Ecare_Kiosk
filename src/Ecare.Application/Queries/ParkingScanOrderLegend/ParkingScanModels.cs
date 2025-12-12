using Ecare.Application.Queries.GetChantiers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.ParkingScanOrderLegend
{
    public class ParkingScanModels
    {
        public sealed class ScanResultVm
        {
            public string Slv { get; set; } = "";
            public List<ClientResult> Clients { get; set; } = new();
        }

        public sealed class ClientResult
        {
            public string ClientName { get; set; } = "";
            public string Matricule { get; set; } = "";
            public string ChauffeurName { get; set; } = "";
            public string CodeSapClient { get; set; } = "";

            public OrderLegendVm? Order { get; set; }
            public List<ChantierVm> Chantiers { get; set; } = new();
        }

        public sealed class OrderLegendVm
        {
            public string CodeSapCommande { get; set; } = "";
            public string BonDeCommande { get; set; } = "";
            public string Matricule { get; set; } = "";
            public string Produit1 { get; set; } = "";
            public string Produit2 { get; set; } = "";
            public decimal? Quantite1 { get; set; }
            public decimal? Quantite2 { get; set; }
            public decimal? PremierePoid { get; set; }
            public decimal? DeuxiemePoid { get; set; }
            public int Step { get; set; }
            public string CodeSapClient { get; set; } = "";
            public DateTime? StartChargingAt { get; set; }
            public string RFIDCard { get; set; } = "";
            public string? Produit1Image { get; set; }
            public string? Produit2Image { get; set; }
            public string ClientName { get; set; } = "";
            public string? ChauffeurName { get; set; } = "";
        }

        public sealed class ChantierVm
        {
            public string CodeSapChantier { get; set; } = "";
            public string NomChantier { get; set; } = "";
        }

        public sealed class SapRequest
        {
            public string CodeClient { get; set; } = "";
            public string CodeSite { get; set; } = "MA18";
            public string IV_VTWEG { get; set; } = "01";
            public string IV_SPART { get; set; } = "01";
            public bool OnlyShipTo { get; set; } = true;
        }

        public sealed class SapResponse
        {
            public int count { get; set; }
            public List<SapChantier> chantiers { get; set; } = new();
        }

        public sealed class SapChantier
        {
            public string kunN2 { get; set; } = "";  // Code SAP Chantier
            public string namE1 { get; set; } = "";  // Nom Chantier
        }


    }
}
