using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.PartnerTruck
{
    public sealed class GetPartnerTrucksHandler
    : IRequestHandler<GetPartnerTrucksQuery, Result<PartnerTruckPagedResult>>
    {
        private readonly IDbConnectionFactory _factory;

        public GetPartnerTrucksHandler(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<Result<PartnerTruckPagedResult>> Handle(GetPartnerTrucksQuery request, CancellationToken ct)
        {
            using var conn = _factory.Create();
            await ((dynamic)conn).OpenAsync(ct);

            var param = new DynamicParameters();
            param.Add("@Page", request.Page);
            param.Add("@PageSize", request.PageSize);

            var sql = "sp_PartnerTruck_List";

            var list = await conn.QueryAsync<PartnerTruckPagedTemp>(sql, param,
                commandType: CommandType.StoredProcedure);

            if (!list.Any())
            {
                return Result<PartnerTruckPagedResult>.Ok(new PartnerTruckPagedResult
                {
                    TotalCount = 0,
                    Items = Array.Empty<PartnerTruckVm>()
                });
            }

            int total = list.First().TotalCount;

            var vms = list.Select(x => new PartnerTruckVm
            {
                Code = x.Code,
                Name = x.Name,
                Matricule = x.Matricule,
                TruckType = x.TruckType,
                PartnerType = x.PartnerType,
                TypePartnerName = x.TypePartnerName
            });

            return Result<PartnerTruckPagedResult>.Ok(new PartnerTruckPagedResult
            {
                TotalCount = total,
                Items = vms
            });
        }

        private sealed class PartnerTruckPagedTemp
        {
            public int TotalCount { get; set; }
            public string Code { get; set; } = default!;
            public string Name { get; set; } = default!;
            public string Matricule { get; set; } = default!;
            public string TruckType { get; set; } = default!;
            public int PartnerType { get; set; }
            public string TypePartnerName { get; set; } = default!;
        }
    }
}
