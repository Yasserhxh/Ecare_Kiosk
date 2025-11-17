using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Ecare.Domain.ValueObjects.ParkingScanDtos;

namespace Ecare.Application.Queries.ParkingScan
{
    public sealed class ScanByRfidHandler : IRequestHandler<ScanByRfidQuery, ScanResult>
    {
        private readonly string _conn;

        public ScanByRfidHandler(IConfiguration cfg)
        {
            _conn = cfg.GetConnectionString("SqlServer")
                ?? throw new InvalidOperationException("Missing ConnString 'SqlServer'");
        }

        public async Task<ScanResult> Handle(ScanByRfidQuery request, CancellationToken ct)
        {
            using var con = new SqlConnection(_conn);

            var raw = await con.QueryFirstOrDefaultAsync<dynamic>(
                "sp_GetScanInfoByRfid",
                new { Rfid = request.Rfid },
                commandType: System.Data.CommandType.StoredProcedure
            );

            if (raw == null)
                return new ScanResult { Code = "ERROR" };

            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            return new ScanResult
            {
                Code = raw.Code,
                Truck = raw.Truck is string t ? JsonSerializer.Deserialize<TruckDto>(t, opt) : null,
                Clients = raw.Clients is string c ? JsonSerializer.Deserialize<List<ClientDto>>(c, opt) ?? new() : new(),
                Chantiers = raw.Chantiers is string ch ? JsonSerializer.Deserialize<List<ChantierDto>>(ch, opt) ?? new() : new(),
                Order = raw.OrderData is string o ? JsonSerializer.Deserialize<OrderDto>(o, opt) : null,
                Items = raw.Items is string it ? JsonSerializer.Deserialize<List<OrderItemDto>>(it, opt) ?? new() : new()
            };
        }
    }
}
