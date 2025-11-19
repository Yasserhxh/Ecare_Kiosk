using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Queries.LegendSnapshot;

public sealed record LegendSnapshotQuery(string RfidCard)
    : IRequest<Result<LegendSnapshotVm>>;

public sealed class LegendSnapshotVm
{
    public int LegendId { get; set; }

    // Legend fields
    public string ClientName { get; set; } = string.Empty;
    public string Chantier { get; set; } = string.Empty;
    public string Matricule { get; set; } = string.Empty;
    public int RFIDCard { get; set; }
    public string TypeCamion { get; set; } = string.Empty;
    public string BonDeCommande { get; set; } = string.Empty;
    public string Ligne { get; set; } = string.Empty;
    public int? PremierePoid { get; set; }
    public int? DeuxiemePoid { get; set; }

    public DateTime? ParkingAt { get; set; }
    public DateTime? PabEntryAt { get; set; }
    public DateTime? StartChargingAt { get; set; }
    public DateTime? FinishedChargingAt { get; set; }
    public DateTime? PabExitAt { get; set; }
    public int Step { get; set; }

    // Driver
    public int? DriverId { get; set; }
    public string DriverNom { get; set; } = string.Empty;
    public string DriverPrenom { get; set; } = string.Empty;
    public string DriverFullName { get; set; } = string.Empty;

    // Truck
    public int? TruckId { get; set; }
    public string TruckPlate { get; set; } = string.Empty;
    public string TruckTypeName { get; set; } = string.Empty;
    public string? TruckTypeImage { get; set; }

    // Products
    public string Produit1 { get; set; } = string.Empty;
    public int? Quantite1 { get; set; }
    public string? Produit1Image { get; set; }

    public string Produit2 { get; set; } = string.Empty;
    public int? Quantite2 { get; set; }
    public string? Produit2Image { get; set; }

    // Ligne image
    public string? LigneImage { get; set; }
}
