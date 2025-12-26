using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetDrivers
{
    public sealed class GetDriversDropdownHandler
     : IRequestHandler<GetDriversDropdownQuery, Result<IEnumerable<DriverDropdownVm>>>
    {
        private readonly IDbConnectionFactory _factory;

        public GetDriversDropdownHandler(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<Result<IEnumerable<DriverDropdownVm>>> Handle(GetDriversDropdownQuery request, CancellationToken ct)
        {
            const string sql = @"
            SELECT 
                Id,
                (Nom + ' ' + Prenom) AS FullName
            FROM Ecare_Driver
            ORDER BY Nom, Prenom;
        ";

            using var conn = _factory.Create();
            if (conn.State != ConnectionState.Open)
                await ((dynamic)conn).OpenAsync(ct);

            var data = await conn.QueryAsync<DriverDropdownVm>(sql);

            return Result<IEnumerable<DriverDropdownVm>>.Ok(data);
        }
    }
}
