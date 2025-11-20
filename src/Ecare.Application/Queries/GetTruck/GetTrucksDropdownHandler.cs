using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetTruck
{
    public sealed class GetTrucksDropdownHandler
    : IRequestHandler<GetTrucksDropdownQuery, Result<IEnumerable<TruckDropdownVm>>>
    {
        private readonly IDbConnectionFactory _factory;

        public GetTrucksDropdownHandler(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<Result<IEnumerable<TruckDropdownVm>>> Handle(GetTrucksDropdownQuery request, CancellationToken ct)
        {
            const string sql = @"
            SELECT Id, Matricule
            FROM Ecare_Truck
            WHERE Actif = 1
            ORDER BY Matricule;
        ";

            using var conn = _factory.Create();
            await ((dynamic)conn).OpenAsync(ct);

            var list = await conn.QueryAsync<TruckDropdownVm>(sql);

            return Result<IEnumerable<TruckDropdownVm>>.Ok(list);
        }
    }
}
