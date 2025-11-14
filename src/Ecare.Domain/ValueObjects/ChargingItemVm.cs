using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public sealed record ChargingItemVm(
    int ProductId,
    decimal Quantity,
    string Type,
    string Name);

    public sealed record ChargingDetailsVm(
        int CarteSlv,
        decimal? FirstWeight,
        string Matricule,
        string ClientName,
        IReadOnlyList<ChargingItemVm> Items);
}
