using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.PartnerTruck
{
    public sealed record GetPartnerTrucksQuery(
    int Page,
    int PageSize
) : IRequest<Result<PartnerTruckPagedResult>>;
}
