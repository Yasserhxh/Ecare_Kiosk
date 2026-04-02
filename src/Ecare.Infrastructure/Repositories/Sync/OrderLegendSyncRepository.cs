using Dapper;
using Ecare.Domain.Inerface.Sync;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Ecare.Infrastructure.Repositories.Sync;

public sealed class OrderLegendSyncRepository : IOrderLegendSyncRepository
{
    private readonly string _connectionString;

    public OrderLegendSyncRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("ConnectionStrings:SqlServer is missing.");
    }

    public async Task<IReadOnlyList<PendingOrderSyncModel>> GetPendingOrdersAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (@Take)
                  [Id]
                , [CodeSapClient]
                , [CodeSapCommande]
                , [CreatedAt]
            FROM [dbo].[Ecare_Order_Legend]
            WHERE [Step] = 0
              AND [BonDeLivraison] IS NULL
              AND [IsSynced] = 0
              AND [CodeSapClient] IS NOT NULL
              AND [CodeSapCommande] IS NOT NULL
            ORDER BY [CreatedAt] ASC, [Id] ASC;
            """;

        await using var connection = new SqlConnection(_connectionString);

        var rows = await connection.QueryAsync<PendingOrderSyncModel>(
            new CommandDefinition(
                sql,
                new { Take = take },
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<bool> MarkAsSyncedAsync(
        long id,
        string bonDeLivraison,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [dbo].[Ecare_Order_Legend]
            SET [BonDeLivraison] = @BonDeLivraison,
                [Step] = 5,
                [IsSynced] = 1,
                [DocumentUpdatedAt] = SYSUTCDATETIME(),
                [Status] = 'Completed'
            WHERE [Id] = @Id
              AND [Step] < 5
              AND [BonDeLivraison] IS NULL
              AND [IsSynced] = 0;
            """;

        await using var connection = new SqlConnection(_connectionString);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    Id = id,
                    BonDeLivraison = bonDeLivraison
                },
                cancellationToken: cancellationToken));

        return affected > 0;
    }
}