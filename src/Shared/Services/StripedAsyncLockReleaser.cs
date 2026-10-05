namespace HappyHeadlines.Shared;

internal sealed class StripedAsyncLockReleaser(SemaphoreSlim semaphore) : IDisposable
{
    private SemaphoreSlim? _semaphore = semaphore;
    public void Dispose() => Interlocked.Exchange(ref _semaphore, null)?.Release();
}
