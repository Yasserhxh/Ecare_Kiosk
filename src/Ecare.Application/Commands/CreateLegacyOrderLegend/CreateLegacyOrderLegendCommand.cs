using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.CreateLegacyOrderLegend
{
    public sealed record CreateLegacyOrderLegendCommand(
    string Event,
    string BonDeCommande,
    string ClientName,
    string Chantier,
    string Matricule,
    int RFIDCard,
    string TypeCamion,
    int NombrePlombs,
    string Produit1,
    string CodeSapProduit1,
    int Quantite1,
    string? Produit2,
    string CodeSapProduit2,
    int? Quantite2,
    string TypeProduit,
    string? ChequeImg,        
    DateTime? AddedToQueueAt,
    string CodeClientSap,
    string CodeChantierSap
) : IRequest<Result<int>>;

}
