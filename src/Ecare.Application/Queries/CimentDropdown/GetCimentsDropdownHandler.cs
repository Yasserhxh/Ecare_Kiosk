using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.CimentDropdown
{
    public sealed class GetCimentsDropdownHandler
    : IRequestHandler<GetCimentsDropdownQuery, Result<IEnumerable<CimentDropdownVm>>>
    {
        private readonly IDbConnectionFactory _factory;

        public GetCimentsDropdownHandler(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<Result<IEnumerable<CimentDropdownVm>>> Handle(GetCimentsDropdownQuery request, CancellationToken ct)
        {
            const string sql = @"
            SELECT Id, Name
            FROM EcareCiments
            WHERE IsActive = 1
            ORDER BY Name;
        ";

            using var conn = _factory.Create();
            await ((dynamic)conn).OpenAsync(ct);

            var items = await conn.QueryAsync<CimentDropdownVm>(sql);

            return Result<IEnumerable<CimentDropdownVm>>.Ok(items);
        }
    }
}
