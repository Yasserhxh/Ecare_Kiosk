using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.ToggleLigneStatus
{
    public sealed record ToggleLigneStatusCommand(int LigneId)
    : IRequest<Result<int>>;
}
