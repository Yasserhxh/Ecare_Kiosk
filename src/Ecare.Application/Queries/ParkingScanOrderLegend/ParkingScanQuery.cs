using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Ecare.Application.Queries.ParkingScanOrderLegend.ParkingScanModels;

namespace Ecare.Application.Queries.ParkingScanOrderLegend
{
    public sealed record ParkingScanQuery(string Slv)
    : IRequest<Result<ScanResultVm>>;

}
