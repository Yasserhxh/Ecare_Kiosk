using Ecare.Application.Queries.GetDrivers;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetChantiers
{
    public sealed record GetChantiersQuery(
    int Page,
    int PageSize,
    string? Code,
    string? Name,
    int? PartnerId
) : IRequest<Result<PagedResult<ChantierVm>>>;

    public sealed class ChantierVm
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public DateTime DateCreation { get; set; }
        public bool Actif { get; set; }
        public string? PartnerName { get; set; }
    }

}
