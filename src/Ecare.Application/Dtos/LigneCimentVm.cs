public sealed class LigneCimentVm
{
    public int ZoneId { get; set; }
    public string Usine { get; set; } = default!;
    public string TypeOperation { get; set; } = default!;
    public string TypeActivite { get; set; } = default!;
    public int LigneId { get; set; }
    public string LigneNom { get; set; } = default!;
    public int Capacity { get; set; }
    public int Status { get; set; }
    public int CimentId { get; set; }
    public string CimentName { get; set; } = default!;
    public string CimentType { get; set; } = default!;
    public string CimentDescription { get; set; } = default!;
}
