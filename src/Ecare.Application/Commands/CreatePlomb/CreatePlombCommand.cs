using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CreatePlomb
{
    public sealed record CreatePlombCommand(
     string RFIDCard,
     string Matricule,
     string PlombNumber,
     string Plombs
 ) : IRequest<bool>;
}
