namespace Ecare.Application.Security;

public static class EcareRoles
{
    public const string AgentDeGuichet = "Agent de Guichet";
    public const string Logistique     = "Logistique";
    public const string Expedition     = "Expédition";
    public const string AdminIT        = "Admin IT";
    public const string Admin          = "Admin"; // legacy; treated as Admin IT

    // Equivalent profils (2026-10-08): same permission set as a base profil, kept as
    // distinct AspNetRoles so the org chart stays readable in mycimar-web-client.
    public const string CoordinateurCommercial  = "Coordinateur Commercial";      // = Agent de Guichet
    public const string ChefAgenceLogistique    = "Chef d'Agence Logistique";     // = Logistique
    public const string SuperviseurEnsachage    = "Superviseur Ensachage";        // = Expédition
    public const string ChefEquipeEnsachage     = "Chef d'équipe Ensachage";      // = Expédition
    public const string SurveillantQuaiEnsachage = "Surveillant de quai Ensachage"; // = Expédition
    public const string OperateurVracEnsachage  = "Opérateur vrac Ensachage";     // = Expédition
}
