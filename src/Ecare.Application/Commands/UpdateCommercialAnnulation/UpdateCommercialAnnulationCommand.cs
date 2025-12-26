using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.UpdateCommercialAnnulation
{
    public sealed record UpdateCommercialAnnulationCommand(
    int Id,
    int? AnnulationCommercial,
    string? MotifAnnulationCommercial,
    string? UserIdAnnulationCommercial
) : IRequest<Result<UpdateCommercialAnnulationVm>>;

    public sealed record UpdateCommercialAnnulationVm(
        int Id,
        int? AnnulationCommercial,
        string? MotifAnnulationCommercial,
        string? UserIdAnnulationCommercial
    );
}
