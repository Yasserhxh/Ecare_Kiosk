using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.Partner
{
    public sealed record CreatePartnerCommand(
    string Code,
    string Name,
    string PartnerType
) : IRequest<Result<int>>;
}
