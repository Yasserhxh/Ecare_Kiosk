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
    public sealed class HelloDeviceHandler : IRequestHandler<HelloDeviceCommand, HelloDeviceResult>
    {
        private readonly IConfiguration _cfg;
        public HelloDeviceHandler(IConfiguration cfg) => _cfg = cfg;

        public async Task<HelloDeviceResult> Handle(HelloDeviceCommand req, CancellationToken ct)
        {
            var cs = _cfg.GetConnectionString("SqlServer")
                     ?? throw new InvalidOperationException("ConnectionStrings:SqlServer missing");

            using var db = new SqlConnection(cs);
            await db.OpenAsync(ct);
            using var tx = db.BeginTransaction();

            // Upsert device inventory
            const string upsertDevice = """
                MERGE dbo.Ecare_Device AS t
                USING (SELECT @DeviceId AS DeviceId) AS s
                ON (t.DeviceId = s.DeviceId)
                WHEN MATCHED THEN UPDATE SET
                    HostName = COALESCE(@HostName, t.HostName),
                    Site = COALESCE(@Site, t.Site),
                    AppVersion = COALESCE(@AppVersion, t.AppVersion),
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN INSERT (DeviceId, HostName, Site, AppVersion)
                VALUES (@DeviceId, @HostName, @Site, @AppVersion);
                """;

            await db.ExecuteAsync(new CommandDefinition(upsertDevice, new
            {
                req.DeviceId,
                req.HostName,
                req.Site,
                req.AppVersion
            }, tx, cancellationToken: ct));

            // Upsert heartbeat
            const string upsertHeartbeat = """
                MERGE dbo.Ecare_DeviceHeartbeat AS t
                USING (SELECT @DeviceId AS DeviceId) AS s
                ON (t.DeviceId = s.DeviceId)
                WHEN MATCHED THEN UPDATE SET
                    LastSeenUtc = SYSUTCDATETIME(),
                    IP = COALESCE(@Ip, t.IP),
                    AppVersion = COALESCE(@AppVersion, t.AppVersion)
                WHEN NOT MATCHED THEN INSERT (DeviceId, LastSeenUtc, IP, AppVersion)
                VALUES (@DeviceId, SYSUTCDATETIME(), @Ip, @AppVersion);
                """;

            await db.ExecuteAsync(new CommandDefinition(upsertHeartbeat, new
            {
                req.DeviceId,
                req.Ip,
                req.AppVersion
            }, tx, cancellationToken: ct));

            // Current state
            const string getState = """
                SELECT d.IsActive,
                       a.LineId
                FROM dbo.Ecare_Device d
                OUTER APPLY (
                  SELECT TOP(1) LineId
                  FROM dbo.Ecare_DeviceAssignment
                  WHERE DeviceId = d.DeviceId AND EffectiveTo IS NULL
                ) a
                WHERE d.DeviceId = @DeviceId;
                """;

            var row = await db.QuerySingleOrDefaultAsync(getState, new { req.DeviceId }, tx);
            tx.Commit();

            bool exists = row is not null;
            bool isActive = exists ? (bool)row.IsActive : true;
            int? currentLineId = exists ? (int?)row.LineId : null;

            return new HelloDeviceResult(req.DeviceId, exists, isActive, currentLineId);
        }
    }
}
