using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CreateClientEquipement
{
    public sealed record CreateClientEquipementCommand(
    CreateClientEquipementDto Dto
) : IRequest<int>;

}
