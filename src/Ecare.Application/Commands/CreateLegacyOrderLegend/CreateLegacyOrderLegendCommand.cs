using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CreateLegacyOrderLegend
{
    public sealed record CreateLegacyOrderLegendCommand(
        string BonDeCommande,
        int OrderId,
        string ClientName,
        string Chantier,
        string Matricule,
        int RFIDCard,
        string TypeCamion,
        int NombrePlombs,
        string Produit1,
        int Quantite1,
        string Produit2,
        int Quantite2,
        string TypeProduit,
        DateTime? AddedToQueueAt
    ) : IRequest<Result<int>>;
}
