using Dapper;
using Ecare.Application.Queries.GetDrivers;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetPartner
{
    public sealed class GetPartnersHandler
    : IRequestHandler<GetPartnersQuery, Result<PagedResult<PartnerVm>>>
    {
        private readonly IDbConnectionFactory _factory;

        public GetPartnersHandler(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<Result<PagedResult<PartnerVm>>> Handle(GetPartnersQuery request, CancellationToken ct)
        {
            using var conn = _factory.Create();
            await ((dynamic)conn).OpenAsync(ct);

            var where = new StringBuilder(" WHERE 1=1 ");
            var param = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(request.Code))
            {
                where.Append(" AND Code LIKE @Code ");
                param.Add("@Code", $"%{request.Code}%");
            }
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                where.Append(" AND Name LIKE @Name ");
                param.Add("@Name", $"%{request.Name}%");
            }
            if (!string.IsNullOrWhiteSpace(request.PartnerType))
            {
                where.Append(" AND PartnerType LIKE @PartnerType ");
                param.Add("@PartnerType", $"%{request.PartnerType}%");
            }

            string sqlCount = $"SELECT COUNT(*) FROM Ecare_Partner {where}";
            string sqlData = $@"
            SELECT 
                Id, Code, Name, PartnerType, DateCreation, Actif
            FROM Ecare_Partner
            {where}
            ORDER BY Id DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
        ";

            param.Add("@Skip", (request.Page - 1) * request.PageSize);
            param.Add("@Take", request.PageSize);

            var total = await conn.ExecuteScalarAsync<int>(sqlCount, param);
            var items = await conn.QueryAsync<PartnerVm>(sqlData, param);

            return Result<PagedResult<PartnerVm>>.Ok(new PagedResult<PartnerVm>
            {
                Items = items,
                TotalCount = total
            });
        }
    }
}
