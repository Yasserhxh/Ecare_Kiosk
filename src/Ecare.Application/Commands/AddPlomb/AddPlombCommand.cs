using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.AddPlomb
{
    public sealed record AddPlombCommand(
    string RfidCard,
    string Matricule,
    string PlombNumber,
    string Plombs
) : IRequest<bool>;
}
