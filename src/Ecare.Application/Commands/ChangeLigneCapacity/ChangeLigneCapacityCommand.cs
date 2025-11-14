using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.ChangeLigneCapacity
{
    public sealed record ChangeLigneCapacityCommand(int LigneId, int Capacity)
    : IRequest<Result<int>>;
}
