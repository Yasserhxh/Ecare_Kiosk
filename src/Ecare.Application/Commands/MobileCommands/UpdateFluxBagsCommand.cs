using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.MobileCommands
{
    public sealed record UpdateFluxBagsCommand(int Id, decimal MinusBag, decimal PlusBag)
    : IRequest<Result<int>>;
}
