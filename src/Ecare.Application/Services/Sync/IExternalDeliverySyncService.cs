namespace Ecare.Application.Services.Sync;

public interface IExternalDeliverySyncService
{
    Task SyncAsync(CancellationToken cancellationToken = default);
}