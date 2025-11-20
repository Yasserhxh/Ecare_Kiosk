using Dapper;
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
    public sealed class GetChantiersHandler
    : IRequestHandler<GetChantiersQuery, Result<PagedResult<ChantierVm>>>
    {
        private readonly IDbConnectionFactory _factory;

        public GetChantiersHandler(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<Result<PagedResult<ChantierVm>>> Handle(GetChantiersQuery request, CancellationToken ct)
        {
            using var conn = _factory.Create();
            await ((dynamic)conn).OpenAsync(ct);

            var sb = new StringBuilder(" WHERE 1 = 1 ");
            var param = new DynamicParameters();

            // Optional filters
            if (!string.IsNullOrWhiteSpace(request.Code))
            {
                sb.Append(" AND c.Code LIKE @Code ");
                param.Add("@Code", $"%{request.Code}%");
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                sb.Append(" AND c.Name LIKE @Name ");
                param.Add("@Name", $"%{request.Name}%");
            }

            if (request.PartnerId.HasValue)
            {
                sb.Append(" AND c.ClientId = @PartnerId ");
                param.Add("@PartnerId", request.PartnerId.Value);
            }

            // COUNT query
            string sqlCount = $@"
            SELECT COUNT(*)
            FROM Ecare_Chantier c
            LEFT JOIN Ecare_Partner p ON p.Id = c.ClientId
            {sb};
        ";

            // DATA query
            string sqlData = $@"
            SELECT 
                c.Id,
                c.Code,
                c.Name,
                c.DateCreation,
                c.Actif,
                p.Name AS PartnerName
            FROM Ecare_Chantier c
            LEFT JOIN Ecare_Partner p ON p.Id = c.ClientId
            {sb}
            ORDER BY c.Id DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
        ";

            param.Add("@Skip", (request.Page - 1) * request.PageSize);
            param.Add("@Take", request.PageSize);

            var total = await conn.ExecuteScalarAsync<int>(sqlCount, param);
            var items = await conn.QueryAsync<ChantierVm>(sqlData, param);

            return Result<PagedResult<ChantierVm>>.Ok(new PagedResult<ChantierVm>
            {
                Items = items,
                TotalCount = total
            });
        }
    }
}
