namespace Ecare.Domain.Inerface.Sync;
public interface ISyncExecutionLock : IAsyncDisposable
{
    bool Acquired { get; }
}