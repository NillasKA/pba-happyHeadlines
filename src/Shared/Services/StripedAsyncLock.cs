namespace HappyHeadlines.Shared;

/// <summary>Bounded in-process request coalescing for concurrent cache misses.</summary>
public sealed class StripedAsyncLock
{
    private readonly SemaphoreSlim[] _stripes = Enumerable.Range(0, 64)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    public async ValueTask<IDisposable> LockAsync(string key, CancellationToken cancellationToken)
    {
        var stripe = _stripes[(int)((uint)StringComparer.Ordinal.GetHashCode(key) % (uint)_stripes.Length)];
        await stripe.WaitAsync(cancellationToken);
        return new StripedAsyncLockReleaser(stripe);
    }
}
