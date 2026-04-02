namespace Ecare.Domain.Inerface.Sync;

public interface IOrderLegendSyncRepository
{
    Task<IReadOnlyList<PendingOrderSyncModel>> GetPendingOrdersAsync(
        int take,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsSyncedAsync(
        long id,
        string bonDeLivraison,
        CancellationToken cancellationToken = default);
}