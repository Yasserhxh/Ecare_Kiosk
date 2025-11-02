namespace Ecare.Domain.Entities;
public class Client
{
    public Guid Client_Id { get; set; }
    public Guid? User_Id { get; set; }
    public string? Nom { get; set; }
    public string? Prenom { get; set; }
    public string? RaisonSociale { get; set; }
    public int? FormeJuridique_Id { get; set; }
    public string? Ice { get; set; }
    public string? Nom_Complet { get; set; }
    public string? Email { get; set; }
    public bool? IsEmailConfirmed { get; set; }
    public string? Adresse { get; set; }
    public string? Telephone { get; set; }
    public int? IdVille { get; set; }
    public DateTime? DateInscription { get; set; }
    public string? Vérifié_Par { get; set; }
    public string? CodeClientSap { get; set; }
    public string? Cnie { get; set; }
    public string? Gsm { get; set; }
    public string? Destinataire_Interlocuteur { get; set; }
    public Guid? Client_Ctn_Id { get; set; }
    public bool? A_accès_Créances { get; set; }
    public bool? A_accès_Livraison_Non_Facturées { get; set; }
    public bool? A_accès_En_cours { get; set; }
    public bool? A_accès_Degré_Depuisent { get; set; }
    public bool? A_accès_Téléchargement_Factures { get; set; }
    public bool? A_accès_Demande_Devis { get; set; }
    public bool? A_accès_Plafond { get; set; }
    public bool? A_accès_Effets_Non_Échus { get; set; }
    public bool? A_accès_Statut_Compte { get; set; }
    public bool? A_accès_Liste_Règlements { get; set; }
    public bool? A_accès_Liste_Factures { get; set; }
    public bool? A_accès_Liste_Livraisons { get; set; }
    public int? IdPays { get; set; }

    // Computed properties
    public string Name => Nom_Complet ?? $"{Nom} {Prenom}".Trim();
    public string? SapCode => CodeClientSap;
    public bool SapOk => !string.IsNullOrWhiteSpace(CodeClientSap);
}
