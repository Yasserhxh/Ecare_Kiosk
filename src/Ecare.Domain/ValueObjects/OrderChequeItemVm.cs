using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public sealed record OrderChequeItemVm(
    int OrderId,
    string? ChequeImageUrl,
    string ClientName,
    DateTime ParkedAt,
    string DriverName,
    int CarteSlv);

    public sealed record OrderChequesPageVm(
        IReadOnlyList<OrderChequeItemVm> Items,
        int Page,
        int PageSize,
        int TotalCount);
}
