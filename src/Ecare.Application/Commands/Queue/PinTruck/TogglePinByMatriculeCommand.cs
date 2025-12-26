using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.Queue.PinTruck
{
    public sealed record TogglePinByMatriculeCommand(string Matricule)
    : IRequest<Result<int>>;
}
