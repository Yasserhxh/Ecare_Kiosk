using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.ChantierDropdowByIdClient
{
    public sealed class GetChantiersDropdownByPartnerHandler
    : IRequestHandler<GetChantiersDropdownByPartnerQuery, Result<IEnumerable<ChantierDropdownVm>>>
    {
        private readonly IDbConnectionFactory _factory;

        public GetChantiersDropdownByPartnerHandler(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<Result<IEnumerable<ChantierDropdownVm>>> Handle(
            GetChantiersDropdownByPartnerQuery request,
            CancellationToken ct)
        {
            const string sql = @"
            SELECT 
                Id,
                (Code + ' - ' + Name) AS DisplayName
            FROM Ecare_Chantier
            WHERE ClientId = @PartnerId
              AND Actif = 1
            ORDER BY Name;
        ";

            using var conn = _factory.Create();
            await ((dynamic)conn).OpenAsync(ct);

            var result = await conn.QueryAsync<ChantierDropdownVm>(
                sql,
                new { request.PartnerId }
            );

            return Result<IEnumerable<ChantierDropdownVm>>.Ok(result);
        }
    }
}
