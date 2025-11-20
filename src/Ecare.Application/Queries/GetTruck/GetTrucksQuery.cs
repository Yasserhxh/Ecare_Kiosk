using Ecare.Application.Commands.CreateEcareTruck;
using Ecare.Application.Queries.GetDrivers;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetTruck
{
    public sealed record GetTrucksQuery(
    int Page,
    int PageSize,
    string? Matricule,
    int? TruckTypeId,
    int? DriverId
) : IRequest<Result<PagedResult<EcareTruckFullVm>>>;
}
