namespace Ecare.Application.Queries.Legend;

public sealed class LegendBusinessRow
{
    // Mapping
    public int Id { get; set; }
    public DateTime? DateCreation { get; set; }           // COALESCE(ParkingAt, CreatedAt) — legacy
    public DateTime? CreatedAt { get; set; }              // real record/commande creation date
    public DateTime? DateAffectation { get; set; }        // when commande affected to matricule (merge)
    public string? Matricule { get; set; }
    public string? RFID { get; set; }                    // RFIDCard
    public string? ClientName { get; set; }
    public string? TransporteurName { get; set; }
    public string? ChauffeurName { get; set; }
    public string? Circuit { get; set; }                 // TypeProduit
    public string TypeOperation { get; set; } = "C";     // constant
    public string? CodeChantier { get; set; }            // CodeSapChantier
    public string? NomChantier { get; set; }             // Chantier
    public string? CodeArticle { get; set; }             // CodeSapProduit1
    public string? Article { get; set; }                 // Produit1
    public string? Article2 { get; set; }
    public decimal? Quantite1 { get; set; }
    public decimal? Quantite2 { get; set; }
    public string? Ligne { get; set; }
    public string? TypeCamion { get; set; }

    public decimal? Tare { get; set; }                   // PremierePoid
    public decimal? Gross { get; set; }                  // DeuxiemePoid
    public decimal? Net { get; set; }                    // Gross - Tare
    public decimal? PTAC { get; set; }
    public decimal? TAREVehicule { get; set; }

    public int Step { get; set; }                         // Statut
    public string? CommandeSap { get; set; }              // CodeSapCommande
    public string? LivraisonSap { get; set; }             // BonDeLivraison

    public string ScaleGross { get; set; } = "WO";        // constant
    public string ScaleTare { get; set; } = "WI";         // constant

    public string TypeLivraison { get; set; } = default!; // CFR / EXW
    public int? Seals { get; set; }                       // PlombNumber
    public string? BonCommandeClient { get; set; }        // BonDeCommande

    public string TypeCommande { get; set; } = default!;  // Simple / Mixte
    public int? SacNumber { get; set; }
    public int? NumberSacs_Charged { get; set; }
    public decimal? Weight_Charged { get; set; }
    public DateTime? ParkingAt { get; set; }
    public DateTime? PabEntryAt { get; set; }
    public DateTime? StartChargingAt { get; set; }
    public DateTime? FinishedChargingAt { get; set; }
    public DateTime? PabExitAt { get; set; }
    public int? ElapsedTimeParking { get; set; }
    public int? ElapsedInPabCharging { get; set; }
    public int? ElapsedCharging { get; set; }
    public int? ElapsedTimeInFExit { get; set; }
    public int? TotalTimeInCercuit { get; set; }
    public DateTime? AddedToQueueAt { get; set; }
    public DateTime? FirstPlaceAt { get; set; }
    public int? TimeElapsedInFirstPlace { get; set; }
    public DateTime? StartExtraSac { get; set; }
    public DateTime? EndExtraSac { get; set; }
    public int? ElapsedExtraSac { get; set; }
    public int? AnnulationCommercial { get; set; }
    public string? MotifAnnulationCommercial { get; set; }
    public string? UserIdAnnulationCommercial { get; set; }
    public bool IsLowCreditDeliveryRisk { get; set; }
}
