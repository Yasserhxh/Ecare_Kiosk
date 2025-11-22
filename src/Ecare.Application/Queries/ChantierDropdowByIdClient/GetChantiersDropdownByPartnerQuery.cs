using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.ChantierDropdowByIdClient
{
    public sealed record GetChantiersDropdownByPartnerQuery(
    int PartnerId
    ) : IRequest<Result<IEnumerable<ChantierDropdownVm>>>;
}
