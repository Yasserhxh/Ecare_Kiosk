using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.UnlinkPartnerFromTruck
{
    public sealed record UnlinkPartnerFromTruckCommand(int PartnerId)
    : IRequest<Result<string>>;
}
