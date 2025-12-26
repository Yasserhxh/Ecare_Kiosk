using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetTruck
{
    public sealed record GetTrucksDropdownQuery()
    : IRequest<Result<IEnumerable<TruckDropdownVm>>>;
}
