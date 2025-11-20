using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.ChantierDropdown
{
    public sealed record GetChantiersDropdownQuery()
    : IRequest<Result<IEnumerable<ChantierDropdownVm>>>;
}
