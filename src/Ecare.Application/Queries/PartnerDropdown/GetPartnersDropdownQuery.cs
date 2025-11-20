using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.PartnerDropdown
{
    public sealed record GetPartnersDropdownQuery()
    : IRequest<Result<IEnumerable<PartnerDropdownVm>>>;
}
