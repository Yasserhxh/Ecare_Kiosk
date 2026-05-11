using MediatR;
using Ecare.Shared;

namespace Ecare.Application.Queries.PabEntryScan
{
    public sealed record PabEntryScanQuery(string RfidCard)
        : IRequest<Result<PabEntryScanVm>>;

    public sealed class PabEntryScanVm
    {
        public int? LegendId { get; set; }
        public string? BonDeCommande { get; set; }
        public int? PTAC {  get; set; }
        public int? Tare {  get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string Chantier { get; set; } = string.Empty;

        public string Matricule { get; set; } = string.Empty;
        public int RFIDCard { get; set; }

        public int DriverId { get; set; }
        public string FullName { get; set; } = string.Empty;

        public int PremierePoid { get; set; }

        public string? Produit1 { get; set; }
        public double Quantite1 { get; set; }
        public string? Produit2 { get; set; }
        public double? Quantite2 { get; set; }

        public string? Image1 { get; set; }
        public string? Image2 { get; set; }
    }
}
