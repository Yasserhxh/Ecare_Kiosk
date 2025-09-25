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
    public string? V�rifi�_Par { get; set; }
    public string? CodeClientSap { get; set; }
    public string? Cnie { get; set; }
    public string? Gsm { get; set; }
    public string? Destinataire_Interlocuteur { get; set; }
    public Guid? Client_Ctn_Id { get; set; }
    public bool? A_acc�s_Cr�ances { get; set; }
    public bool? A_acc�s_Livraison_Non_Factur�es { get; set; }
    public bool? A_acc�s_En_cours { get; set; }
    public bool? A_acc�s_Degr�_Depuisent { get; set; }
    public bool? A_acc�s_T�l�chargement_Factures { get; set; }
    public bool? A_acc�s_Demande_Devis { get; set; }
    public bool? A_acc�s_Plafond { get; set; }
    public bool? A_acc�s_Effets_Non_�chus { get; set; }
    public bool? A_acc�s_Statut_Compte { get; set; }
    public bool? A_acc�s_Liste_R�glements { get; set; }
    public bool? A_acc�s_Liste_Factures { get; set; }
    public bool? A_acc�s_Liste_Livraisons { get; set; }
    public int? IdPays { get; set; }

    // Computed properties
    public string Name => Nom_Complet ?? $"{Nom} {Prenom}".Trim();
    public string? SapCode => CodeClientSap;
    public bool SapOk => !string.IsNullOrWhiteSpace(CodeClientSap);
}
