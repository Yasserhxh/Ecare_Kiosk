using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.ToggleLigneCiment
{
    public sealed record ToggleLigneCimentCommand(
        int LigneId,
        int CimentId)
        : IRequest<Result<bool>>;
}
