using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.DeviceCommand
{
    public sealed class GetDeviceAssignmentHandler
    : IRequestHandler<GetDeviceAssignmentQuery, GetDeviceAssignmentResult>
    {
        private readonly IConfiguration _cfg;
        public GetDeviceAssignmentHandler(IConfiguration cfg) => _cfg = cfg;

        public async Task<GetDeviceAssignmentResult> Handle(GetDeviceAssignmentQuery req, CancellationToken ct)
        {
            var cs = _cfg.GetConnectionString("SqlServer")
                     ?? throw new InvalidOperationException("ConnectionStrings:SqlServer missing");

            const string sql = """
            SELECT TOP(1)
              DeviceId,
              LineId,
              EffectiveFrom AS EffectiveFromUtc,
              EffectiveTo   AS EffectiveToUtc
            FROM dbo.Ecare_DeviceAssignment
            WHERE DeviceId = @DeviceId
            -- Prefer the open row (EffectiveTo IS NULL), otherwise the most recent closed one
            ORDER BY
              CASE WHEN EffectiveTo IS NULL THEN 0 ELSE 1 END,
              EffectiveFrom DESC;
            """;


            using var db = new SqlConnection(cs);
            var row = await db.QuerySingleOrDefaultAsync(sql, new { req.DeviceId });

            return row is null
                ? new GetDeviceAssignmentResult(req.DeviceId, null, null, null)
                : new GetDeviceAssignmentResult(
                    (string)row.DeviceId,
                    (int?)row.LineId,
                    (DateTime?)row.EffectiveFromUtc,
                    (DateTime?)row.EffectiveToUtc
                  );
        }
    }
}
