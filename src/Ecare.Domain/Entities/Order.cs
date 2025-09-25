namespace Ecare.Domain.Entities;
using Ecare.Domain;

public class Order
{
    public int Id { get; set; }
    public int? ShippingId { get; set; }
    public string NumeroCommande { get; set; } = default!;
    public DateTime? DateCommande { get; set; }
    public string? Destination { get; set; }
    public string? ModeDelivraison { get; set; }
    public string? EmplacementChargement { get; set; }
    public string? MethodeChargement { get; set; }
    public DateTime? HeureEnlevement { get; set; }
    public DateTime? HeureDelivraison { get; set; }
    public string? CarteSLV { get; set; }
    public string? ChauffeurNom { get; set; }
    public string? ChauffeurPrenom { get; set; }
    public string? PermisDeConduire { get; set; }
    public string? PlaqueCamion { get; set; }
    public string Statut { get; set; } = "Crée";
    public string? UserId { get; set; }
    public string? Commentaire { get; set; }
    public string? NomComplet { get; set; }

    // Computed properties for backward compatibility
    public string Number => NumeroCommande;
    public DateTime? OrderedAt => DateCommande;
    public string? DeliveryMode => ModeDelivraison;
    public string? LoadingLocation => EmplacementChargement;
    public string? LoadingMethod => MethodeChargement;
    public TimeSpan? PickupTime => HeureEnlevement?.TimeOfDay;
    public TimeSpan? DeliveryTime => HeureDelivraison?.TimeOfDay;
    public string? SlvCard => CarteSLV;
    public string? DriverLastName => ChauffeurNom;
    public string? DriverFirstName => ChauffeurPrenom;
    public string? DriverLicense => PermisDeConduire;
    public string? TruckPlate => PlaqueCamion;
    public OrderStatus Status => Statut switch
    {
        "Crée" => OrderStatus.Cree,
        "Confirmée" => OrderStatus.Confirmee,
        "EnTraitement" => OrderStatus.EnTraitement,
        "Livre" => OrderStatus.Livre,
        "Termine" => OrderStatus.Termine,
        "Annulee" => OrderStatus.Annulee,
        _ => OrderStatus.Cree
    };
    public decimal? TargetQuantityTons { get; set; } 
}
