using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetTruckTypes
{
    public sealed class GetTruckTypesDropdownHandler
    : IRequestHandler<GetTruckTypesDropdownQuery, Result<IEnumerable<TruckTypeDropdownVm>>>
    {
        private readonly IDbConnectionFactory _factory;

        public GetTruckTypesDropdownHandler(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<Result<IEnumerable<TruckTypeDropdownVm>>> Handle(GetTruckTypesDropdownQuery request, CancellationToken ct)
        {
            const string sql = @"
            SELECT Id, Type
            FROM Ecare_Truck_Type
            ORDER BY Type;
        ";

            using var conn = _factory.Create();
            await ((dynamic)conn).OpenAsync(ct);

            var list = await conn.QueryAsync<TruckTypeDropdownVm>(sql);

            return Result<IEnumerable<TruckTypeDropdownVm>>.Ok(list);
        }
    }
}
