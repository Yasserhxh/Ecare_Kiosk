using Dapper;
using Ecare.Application.Commands.EcareDriver;
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
    using Dapper;
    using MediatR;
    using Ecare.Shared;
    using System.Text;

    public sealed class GetDriversHandler
    : IRequestHandler<GetDriversQuery, Result<PagedResult<EcareDriver>>>
    {
        private readonly IDbConnectionFactory _factory;

        public GetDriversHandler(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<Result<PagedResult<EcareDriver>>> Handle(GetDriversQuery request, CancellationToken ct)
        {
            using var conn = _factory.Create();
            if (conn.State != ConnectionState.Open)
                await ((dynamic)conn).OpenAsync(ct);

            var where = new StringBuilder(" WHERE 1=1 ");
            var param = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(request.Cin))
            {
                where.Append(" AND Cin LIKE @Cin ");
                param.Add("@Cin", $"%{request.Cin}%");
            }
            if (!string.IsNullOrWhiteSpace(request.Nom))
            {
                where.Append(@" AND (
                    ISNULL(Nom_Complet, '') LIKE @Nom
                    OR ISNULL(Nom, '') LIKE @Nom
                    OR ISNULL(Prenom, '') LIKE @Nom
                ) ");
                param.Add("@Nom", $"%{request.Nom}%");
            }
            if (!string.IsNullOrWhiteSpace(request.Numero))
            {
                where.Append(" AND Numero LIKE @Numero ");
                param.Add("@Numero", $"%{request.Numero}%");
            }

            string sqlCount = $"SELECT COUNT(*) FROM Ecare_Driver {where}";
            string sqlData = $@"
            SELECT * FROM Ecare_Driver
            {where}
            ORDER BY Id DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
        ";

            sqlData = $@"
            SELECT
                Id,
                Cin,
                Nom,
                Prenom,
                Numero,
                Permis,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(Nom_Complet)), ''),
                    NULLIF(LTRIM(RTRIM(CONCAT(ISNULL(Prenom, ''), ' ', ISNULL(Nom, '')))), '')
                ) AS NomComplet
            FROM Ecare_Driver
            {where}
            ORDER BY Id DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
        ";

            param.Add("@Skip", (request.Page - 1) * request.PageSize);
            param.Add("@Take", request.PageSize);

            var total = await conn.ExecuteScalarAsync<int>(sqlCount, param);
            var items = await conn.QueryAsync<EcareDriver>(sqlData, param);

            return Result<PagedResult<EcareDriver>>.Ok(new PagedResult<EcareDriver>
            {
                Items = items,
                TotalCount = total
            });
        }
    }

}
