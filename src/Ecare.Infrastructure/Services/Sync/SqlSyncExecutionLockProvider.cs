using Dapper;
using Ecare.Domain.Inerface.Sync;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Ecare.Infrastructure.Services.Sync;

public sealed class SqlSyncExecutionLockProvider : ISyncExecutionLockProvider
{
    private readonly string _connectionString;

    public SqlSyncExecutionLockProvider(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("ConnectionStrings:SqlServer is missing.");
    }

    public async Task<ISyncExecutionLock> TryAcquireAsync(
        string resourceName,
        CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            DECLARE @Result INT;

            EXEC @Result = sp_getapplock
                @Resource = @Resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Session',
                @LockTimeout = 0;

            SELECT @Result;
            """;

        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new { Resource = resourceName },
                cancellationToken: cancellationToken));

        if (result >= 0)
        {
            return new SqlSyncExecutionLock(connection, true);
        }

        await connection.DisposeAsync();
        return new SqlSyncExecutionLock(null, false);
    }
}