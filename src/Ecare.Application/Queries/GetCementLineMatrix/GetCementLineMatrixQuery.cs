using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetCementLineMatrix
{
    public sealed record GetCementLineMatrixQuery(string? Usine)
    : IRequest<Result<IReadOnlyList<CementMatrixRowVm>>>;
}
