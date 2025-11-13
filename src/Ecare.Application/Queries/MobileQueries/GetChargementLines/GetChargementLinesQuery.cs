using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.MobileQueries.GetChargementLines
{
    public sealed record ChargementLineVm(string Nom);

    // Query
    public sealed record GetChargementLinesByTypeQuery(string TypeOperation)
    : IRequest<Result<IReadOnlyList<ChargementLineVm>>>;
}
