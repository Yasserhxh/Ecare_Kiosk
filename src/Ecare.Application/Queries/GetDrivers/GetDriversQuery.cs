using Ecare.Application.Commands.EcareDriver;
using Ecare.Application.Common.Responses;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetDrivers
{
    public sealed record GetDriversQuery(
    int Page,
    int PageSize,
    string? Cin,
    string? Nom,
    string? Numero
) : IRequest<Result<PagedResult<EcareDriver>>>;
}
