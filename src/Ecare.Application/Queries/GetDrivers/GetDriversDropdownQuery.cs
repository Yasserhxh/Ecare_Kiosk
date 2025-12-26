using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetDrivers
{
    public sealed record GetDriversDropdownQuery()
     : IRequest<Result<IEnumerable<DriverDropdownVm>>>;
}
