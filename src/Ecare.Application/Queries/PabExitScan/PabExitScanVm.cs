namespace Ecare.Application.Queries.PabExitScan
{
    public sealed class PabExitScanVm
    {
        public int? LegendId { get; set; }
        // Driver / Truck
        public int DriverId { get; set; }
        public string DriverNom { get; set; } = string.Empty;
        public string DriverPrenom { get; set; } = string.Empty;
        public string DriverFullName { get; set; } = string.Empty;

        public string Plate { get; set; } = string.Empty;
        public string CarteSLV { get; set; } = string.Empty;

        // Client
        public string? ClientName { get; set; }

        // Products
        public string? Produit1 { get; set; }
        public int? Quantite1 { get; set; }
        public string? Produit1Image { get; set; }

        public string? Produit2 { get; set; }
        public int? Quantite2 { get; set; }
        public string? Produit2Image { get; set; }

        // Weights
        public int? PremierePoid { get; set; }
        public int? DeuxiemePoid { get; set; }

        // Timeline
        public DateTime? ParkingAt { get; set; }
        public DateTime? PabEntryAt { get; set; }
        public DateTime? StartChargingAt { get; set; }
        public DateTime? FinishedChargingAt { get; set; }
        public DateTime? PabExitAt { get; set; }

        public decimal? ElapsedTimeParking { get; set; }
        public decimal? ElapsedInPab_Charging { get; set; }
        public decimal? ElapsedTimeInF_Exit { get; set; }
        public decimal? TotalTimeInCercuit { get; set; }

        // Misc
        public string? Ligne { get; set; }
        public int Step { get; set; }
        public string? BonDeCommande { get; set; }
        public string? BonDeLivraison { get; set; }
        public string? Cin { get; set; }
    }
}
