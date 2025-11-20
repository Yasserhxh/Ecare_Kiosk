using Ecare.Application.Queries.GetDrivers;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetPartner
{
    public sealed record GetPartnersQuery(
    int Page,
    int PageSize,
    string? Code,
    string? Name,
    string? PartnerType
) : IRequest<Result<PagedResult<PartnerVm>>>;
}
