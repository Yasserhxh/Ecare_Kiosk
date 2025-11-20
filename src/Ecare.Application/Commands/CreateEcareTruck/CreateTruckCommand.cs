using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CreateEcareTruck
{
    using MediatR;
    using Ecare.Shared;

    public sealed record CreateTruckCommand(
        string Matricule,
        int PTAC,
        int TARE,
        string? RfidCard,
        int? TruckTypeId,
        int? DriverId,
        int NumberOfSeals
    ) : IRequest<Result<int>>;

}
