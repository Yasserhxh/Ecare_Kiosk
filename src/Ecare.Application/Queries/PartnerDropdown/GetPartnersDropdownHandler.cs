using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.PartnerDropdown
{
    public sealed class GetPartnersDropdownHandler
    : IRequestHandler<GetPartnersDropdownQuery, Result<IEnumerable<PartnerDropdownVm>>>
    {
        private readonly IDbConnectionFactory _factory;

        public GetPartnersDropdownHandler(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<Result<IEnumerable<PartnerDropdownVm>>> Handle(GetPartnersDropdownQuery request, CancellationToken ct)
        {
            const string sql = @"
            SELECT 
                Id,
                (Code + ' - ' + Name) AS DisplayName
            FROM Ecare_Partner
            WHERE Actif = 1
            ORDER BY Name;
        ";

            using var conn = _factory.Create();
            await ((dynamic)conn).OpenAsync(ct);

            var data = await conn.QueryAsync<PartnerDropdownVm>(sql);

            return Result<IEnumerable<PartnerDropdownVm>>.Ok(data);
        }
    }
}
