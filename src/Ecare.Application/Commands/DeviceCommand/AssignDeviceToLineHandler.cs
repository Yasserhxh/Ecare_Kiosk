using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.DeviceCommand
{
    public sealed class AssignDeviceToLineHandler
    : IRequestHandler<AssignDeviceToLineCommand, AssignDeviceToLineResult>
    {
        private readonly IConfiguration _cfg;
        public AssignDeviceToLineHandler(IConfiguration cfg) => _cfg = cfg;

        public async Task<AssignDeviceToLineResult> Handle(AssignDeviceToLineCommand req, CancellationToken ct)
        {
            var cs = _cfg.GetConnectionString("SqlServer")
                     ?? throw new InvalidOperationException("ConnectionStrings:SqlServer missing");

            using var db = new SqlConnection(cs);
            await db.OpenAsync(ct);
            using var tx = db.BeginTransaction(IsolationLevel.Serializable);

            // Ensure device row
            const string ensureDevice = """
                IF NOT EXISTS (SELECT 1 FROM dbo.Ecare_Device WHERE DeviceId=@DeviceId)
                  INSERT INTO dbo.Ecare_Device(DeviceId) VALUES (@DeviceId);
                """;
            await db.ExecuteAsync(new CommandDefinition(ensureDevice, new { req.DeviceId }, tx, cancellationToken: ct));

            // Close any open assignment for this device
            const string closeCurrent = """
            UPDATE dbo.Ecare_DeviceAssignment
            SET EffectiveTo = SYSUTCDATETIME()
            WHERE DeviceId=@DeviceId AND EffectiveTo IS NULL;
            """;
            await db.ExecuteAsync(new CommandDefinition(closeCurrent, new { req.DeviceId }, tx, cancellationToken: ct));

            // OPTIONAL: enforce one active device per line (comment block out if you allow multiple)
            const string checkLineBusy = """
                IF EXISTS (
                  SELECT 1 FROM dbo.Ecare_DeviceAssignment
                  WHERE LineId=@LineId AND EffectiveTo IS NULL
                )
                  THROW 50001, 'Line already has an active device', 1;
                """;
            await db.ExecuteAsync(new CommandDefinition(checkLineBusy, new { req.LineId }, tx, cancellationToken: ct));

            // Open new assignment
            const string insertAssign = """
                DECLARE @now DATETIME2(3)=SYSUTCDATETIME();
                INSERT INTO dbo.Ecare_DeviceAssignment(DeviceId, LineId, EffectiveFrom, Reason)
                VALUES(@DeviceId, @LineId, @now, @Reason);
                SELECT @now;
                """;
            var assignedAtUtc = await db.ExecuteScalarAsync<DateTime>(
                new CommandDefinition(insertAssign,
                    new { req.DeviceId, req.LineId, req.Reason }, tx, cancellationToken: ct));

            tx.Commit();
            return new AssignDeviceToLineResult(req.DeviceId, req.LineId, assignedAtUtc);
        }
    }

}
