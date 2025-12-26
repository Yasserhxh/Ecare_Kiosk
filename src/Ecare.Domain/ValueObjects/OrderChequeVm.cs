using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public sealed record OrderChequeVm(
    string? ChequeImageUrl,
    string ClientName,
    DateTime ParkedAt,
    string DriverName,
    int CarteSlv);
}
