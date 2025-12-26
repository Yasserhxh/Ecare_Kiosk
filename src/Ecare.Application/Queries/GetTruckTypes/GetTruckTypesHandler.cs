using Dapper;
using Ecare.Application.Queries.GetDrivers;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetTruckTypes
{
    public sealed class GetTruckTypesHandler
    : IRequestHandler<GetTruckTypesQuery, Result<PagedResult<EcareTruckType>>>
    {
        private readonly IDbConnectionFactory _factory;

        public GetTruckTypesHandler(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<Result<PagedResult<EcareTruckType>>> Handle(GetTruckTypesQuery request, CancellationToken ct)
        {
            using var conn = _factory.Create();
            await ((dynamic)conn).OpenAsync(ct);

            var where = new StringBuilder(" WHERE 1=1 ");
            var param = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                where.Append(" AND Type LIKE @Type ");
                param.Add("@Type", $"%{request.Type}%");
            }

            string sqlCount = $"SELECT COUNT(*) FROM Ecare_Truck_Type {where}";
            string sqlData = $@"
            SELECT * FROM Ecare_Truck_Type
            {where}
            ORDER BY Id DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
        ";

            param.Add("@Skip", (request.Page - 1) * request.PageSize);
            param.Add("@Take", request.PageSize);

            var total = await conn.ExecuteScalarAsync<int>(sqlCount, param);
            var items = await conn.QueryAsync<EcareTruckType>(sqlData, param);

            return Result<PagedResult<EcareTruckType>>.Ok(new PagedResult<EcareTruckType>
            {
                Items = items,
                TotalCount = total
            });
        }
    }
}
