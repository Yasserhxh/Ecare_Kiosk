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
            WHERE [BonDeLivraison] IS NULL
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
            DECLARE @Updated TABLE (Ligne NVARCHAR(150));

            UPDATE [dbo].[Ecare_Order_Legend]
            SET [BonDeLivraison] = @BonDeLivraison,
                [Step] = 5,
                [IsSynced] = 1,
                [DocumentUpdatedAt] = CONVERT(datetime, SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time'),
                [Status] = CASE
                    WHEN ISNULL([AnnulationCommercial], 0) = 1 THEN 'Canceled'
                    ELSE 'Completed'
                END
            OUTPUT inserted.[Ligne] INTO @Updated([Ligne])
            WHERE [Id] = @Id
              AND [BonDeLivraison] IS NULL
              AND [IsSynced] = 0;

            IF @@ROWCOUNT > 0
            BEGIN
                UPDATE L
                SET [RealtimeCapacity] =
                    CASE
                        WHEN ISNULL(L.[RealtimeCapacity], 0) < ISNULL(L.[Capacity], 0)
                            THEN ISNULL(L.[RealtimeCapacity], 0) + 1
                        ELSE ISNULL(L.[Capacity], 0)
                    END
                FROM [dbo].[Ecare_Ligne] L
                WHERE L.[Nom] = (
                        SELECT TOP (1) U.[Ligne]
                        FROM @Updated U
                        WHERE U.[Ligne] IS NOT NULL
                    )
                  AND EXISTS (
                        SELECT 1
                        FROM [dbo].[Ecare_Order_Legend] O
                        WHERE O.[Id] = @Id
                          AND ISNULL(O.[AnnulationCommercial], 0) <> 1
                          AND (
                                O.[PabEntryAt] IS NOT NULL
                                OR O.[PremierePoid] IS NOT NULL
                          )
                    );
            END;
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
