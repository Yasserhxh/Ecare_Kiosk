using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetTruckTypes
{
    public sealed record GetTruckTypesDropdownQuery()
    : IRequest<Result<IEnumerable<TruckTypeDropdownVm>>>;
}
