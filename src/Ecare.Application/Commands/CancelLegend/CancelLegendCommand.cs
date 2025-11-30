using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CancelLegend
{
    public sealed record CancelLegendCommand(int Id) : IRequest<bool>;

}
