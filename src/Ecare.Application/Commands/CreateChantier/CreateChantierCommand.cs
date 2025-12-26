using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CreateChantier
{
    public sealed record CreateChantierCommand(
    string Code,
    string Name,
    int? PartnerId,    // this maps to ClientId in table
    bool Actif         // if you want to control it; otherwise always 1
) : IRequest<Result<int>>;
}
