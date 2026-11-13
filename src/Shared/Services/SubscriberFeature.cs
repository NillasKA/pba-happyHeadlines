using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace HappyHeadlines.Shared;

public interface ISubscriberFeature
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken);
    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken);
}

public sealed class SubscriberFeature(IConnectionMultiplexer redis, ILogger<SubscriberFeature> logger) : ISubscriberFeature
{
    private const string Key = "feature:subscribers:enabled";
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { return await redis.GetDatabase().StringGetAsync(Key).WaitAsync(cancellationToken) == "true"; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Subscriber feature state unavailable; interactions disabled");
            return false;
        }
    }
    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await redis.GetDatabase().StringSetAsync(Key, enabled ? "true" : "false").WaitAsync(cancellationToken);
    }
}
