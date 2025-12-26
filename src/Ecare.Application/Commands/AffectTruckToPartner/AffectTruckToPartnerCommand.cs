using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.AffectTruckToPartner
{
    public sealed record AffectTruckToPartnerCommand(
    int PartnerId,
    int TruckId
) : IRequest<Result<string>>;
}
