using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.ProcessParking
{
    public sealed class ParkingProcessRequest
    {
        public string Event { get; set; } = "";   // ORDER_FOUND | NO_ORDER_NO_CLIENT | CLIENTS_WITH_CHANTIERS

        public string Slv { get; set; } = "";
        public int? LegendId { get; set; }
        public string Matricule { get; set; } = "";
        public string Chauffeur { get; set; } = "";

        public string? ClientName { get; set; }
        public string? Chantier { get; set; }
        public string? CodeSapChantier { get; set; }
        public string? CodeSapClient { get; set; }

        public string? Produit1 { get; set; }
        public decimal? Quantite1 { get; set; }

        public string? Produit2 { get; set; }
        public decimal? Quantite2 { get; set; }

        public string? TypeCamion { get; set; }
        public int? NombrePlombs { get; set; }

        public string? BonDeCommande { get; set; }         
        public int? SacNumber { get; set; }                 

        public string? CodeSapProduit1 { get; set; }      
        public string? CodeSapProduit2 { get; set; } 
        public string? ChequeImage { get; set; }
    }

}
