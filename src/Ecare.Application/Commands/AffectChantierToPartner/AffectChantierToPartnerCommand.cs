using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.AffectChantierToPartner
{
    public sealed record AffectChantierToPartnerCommand(
    int ChantierId,
    int PartnerId
) : IRequest<Result<string>>;
}
