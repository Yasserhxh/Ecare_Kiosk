using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.ChantierDropdown
{
    public sealed class GetChantiersDropdownHandler
     : IRequestHandler<GetChantiersDropdownQuery, Result<IEnumerable<ChantierDropdownVm>>>
    {
        private readonly IDbConnectionFactory _factory;

        public GetChantiersDropdownHandler(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<Result<IEnumerable<ChantierDropdownVm>>> Handle(GetChantiersDropdownQuery request, CancellationToken ct)
        {
            const string sql = @"
            SELECT 
                Id,
                (Code + ' - ' + Name) AS DisplayName
            FROM Ecare_Chantier
            WHERE Actif = 1
            ORDER BY Name;
        ";

            using var conn = _factory.Create();
            await ((dynamic)conn).OpenAsync(ct);

            var items = await conn.QueryAsync<ChantierDropdownVm>(sql);

            return Result<IEnumerable<ChantierDropdownVm>>.Ok(items);
        }
    }
}
