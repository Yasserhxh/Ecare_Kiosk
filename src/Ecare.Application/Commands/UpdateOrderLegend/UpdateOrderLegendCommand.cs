using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.UpdateOrderLegend
{
    public sealed record UpdateOrderLegendCommand(
    int OrderId,
    string ClientName,
    string Chantier,
    string Produit1,
    decimal Quantite1,
    string? Produit2,
    decimal? Quantite2
) : IRequest<Result<string>>;
}
