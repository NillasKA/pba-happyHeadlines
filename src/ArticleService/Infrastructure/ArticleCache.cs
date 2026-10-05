using HappyHeadlines.Shared;
using StackExchange.Redis;

namespace HappyHeadlines.ArticleService.Infrastructure;

public sealed class ArticleCache(IConnectionMultiplexer redis, IConfiguration configuration)
{
    private readonly IDatabase _database = redis.GetDatabase();
    private readonly TimeSpan _ttl = TimeSpan.FromHours(configuration.GetValue("Prefill:ArticleTtlHours", 8));
    private readonly bool _enabled = configuration.GetValue("Caching:ArticleEnabled", true);

    public async Task<(ArticleRecord? Value, bool Available)> GetAsync(
        Guid id, string region, CancellationToken cancellationToken, ILogger logger)
    {
        region = Regions.Normalize(region);
        if (!_enabled)
        {
            HappyMetrics.CacheBypasses.WithLabels("article", region).Inc();
            return (null, true);
        }

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        using var timedOperation = logger.BeginTimedOperation("article-cache-get");
        try
        {
            var value = await _database.StringGetAsync(CacheKeys.Article(id, region));
            if (value.HasValue)
            {
                HappyMetrics.CacheHits.WithLabels("article", region).Inc();
                return (System.Text.Json.JsonSerializer.Deserialize<ArticleRecord>(value.ToString(), JsonDefaults.Options), true);
            }

            HappyMetrics.CacheMisses.WithLabels("article", region).Inc();
            return (null, true);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            HappyMetrics.CacheFallbacks.WithLabels("article", region).Inc();
            using (logger.Here())
                logger.LogWarning(exception, "Article cache unavailable; falling back to PostgreSQL for {ArticleId}", id);
            return (null, false);
        }
        finally
        {
            HappyMetrics.CacheOperationDuration.WithLabels("article", "get")
                .Observe(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds);
        }
    }

    public async Task SetAsync(ArticleRecord article, CancellationToken cancellationToken, ILogger logger)
    {
        if (!_enabled)
            return;

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        using var timedOperation = logger.BeginTimedOperation("article-cache-set");
        try
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(article, JsonDefaults.Options);
            await _database.StringSetAsync(CacheKeys.Article(article.Id, article.Region), payload, _ttl);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            HappyMetrics.CacheFallbacks.WithLabels("article", article.Region).Inc();
            using (logger.Here())
                logger.LogWarning(exception, "Unable to populate article cache for {ArticleId}", article.Id);
        }
        finally
        {
            HappyMetrics.CacheOperationDuration.WithLabels("article", "set")
                .Observe(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds);
        }
    }

    public async Task RemoveAsync(Guid id, string region, CancellationToken cancellationToken, ILogger logger)
    {
        region = Regions.Normalize(region);
        if (!_enabled)
            return;

        try
        {
            await _database.KeyDeleteAsync(CacheKeys.Article(id, region));
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            HappyMetrics.CacheFallbacks.WithLabels("article", region).Inc();
            using (logger.Here())
                logger.LogWarning(exception, "Unable to invalidate article cache for {ArticleId}", id);
        }
    }
}
