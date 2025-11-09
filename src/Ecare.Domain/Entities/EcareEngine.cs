using Ecare.Domain.ValueObjects;

namespace Ecare.Domain.Entities;

public class EcareEngine
{
    public string Matricule { get; set; } = null!; 

    public int? PTAC { get; set; }
    public int? TARE { get; set; }
    public int? Type_Camion { get; set; }
    public int? CarteSlv { get; set; }

    public ProcessType? Type_Process { get; set; }

    public int? Code_Process { get; set; }
    public string? Nom_Process { get; set; }
    public int? Code_Chantier { get; set; }
    public string? Nom_Chantier { get; set; }
    public int? Id_Chauffeur { get; set; }
}
