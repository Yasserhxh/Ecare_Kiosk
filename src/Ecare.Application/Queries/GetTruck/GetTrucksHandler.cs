using Dapper;
using Ecare.Application.Commands.CreateEcareTruck;
using Ecare.Application.Queries.GetDrivers;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetTruck
{
    public sealed class GetTrucksHandler
    : IRequestHandler<GetTrucksQuery, Result<PagedResult<EcareTruckFullVm>>>
    {
        private readonly IDbConnectionFactory _factory;

        public GetTrucksHandler(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<Result<PagedResult<EcareTruckFullVm>>> Handle(GetTrucksQuery request, CancellationToken ct)
        {
            using var conn = _factory.Create();
            await ((dynamic)conn).OpenAsync(ct);

            var where = new StringBuilder(" WHERE 1=1 ");
            var param = new DynamicParameters();

            // Filtering
            if (!string.IsNullOrWhiteSpace(request.Matricule))
            {
                where.Append(" AND t.Matricule LIKE @Matricule ");
                param.Add("@Matricule", $"%{request.Matricule}%");
            }
            if (request.TruckTypeId.HasValue)
            {
                where.Append(" AND t.TruckTypeId = @TruckTypeId ");
                param.Add("@TruckTypeId", request.TruckTypeId);
            }
            if (request.DriverId.HasValue)
            {
                where.Append(" AND t.DriverId = @DriverId ");
                param.Add("@DriverId", request.DriverId);
            }

            string sqlCount = $@"
            SELECT COUNT(*) 
            FROM Ecare_Truck t
            {where}
        ";

            string sqlData = $@"
            SELECT 
                t.Id,
                t.Matricule,
                t.PTAC,
                t.TARE,
                t.RfidCard,
                t.TruckTypeId,
                tt.Type AS TruckType,
                t.DriverId,
                (d.Nom + ' ' + d.Prenom) AS DriverName,
                t.NumberOfSeals,
                t.DateCreation,
                t.Actif
            FROM Ecare_Truck t
            LEFT JOIN Ecare_Truck_Type tt ON tt.Id = t.TruckTypeId
            LEFT JOIN Ecare_Driver d ON d.Id = t.DriverId
            {where}
            ORDER BY t.Id DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
        ";

            param.Add("@Skip", (request.Page - 1) * request.PageSize);
            param.Add("@Take", request.PageSize);

            var total = await conn.ExecuteScalarAsync<int>(sqlCount, param);
            var items = await conn.QueryAsync<EcareTruckFullVm>(sqlData, param);

            return Result<PagedResult<EcareTruckFullVm>>.Ok(new PagedResult<EcareTruckFullVm>
            {
                TotalCount = total,
                Items = items
            });
        }
    }
}
