namespace Ecare.Domain.Entities;

public class ClientEquipement
{
    public int Id { get; set; }
    public string ClientName { get; set; } = default!;
    public string CarteSLV { get; set; } = default!;
    public string Matricule { get; set; } = default!;
    public string ChauffeurName { get; set; } = default!;
}
