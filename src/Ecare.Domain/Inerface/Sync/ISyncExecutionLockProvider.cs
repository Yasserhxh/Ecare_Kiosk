namespace Ecare.Domain.Inerface.Sync;

public interface ISyncExecutionLockProvider
{
    Task<ISyncExecutionLock> TryAcquireAsync(
        string resourceName,
        CancellationToken cancellationToken = default);
}
