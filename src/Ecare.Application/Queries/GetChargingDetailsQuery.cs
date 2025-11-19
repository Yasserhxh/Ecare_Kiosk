using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries
{
    public sealed record GetChargingDetailsQuery(int fluxid)
    : IRequest<Result<ChargingDetailsVm>>;
}
