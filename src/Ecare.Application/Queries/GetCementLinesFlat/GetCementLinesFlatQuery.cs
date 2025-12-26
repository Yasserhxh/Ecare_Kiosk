using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetCementLinesFlat
{
    public sealed record GetCementLinesFlatQuery(string? Usine)
    : IRequest<Result<IReadOnlyList<CementLineFlatVm>>>;
}
