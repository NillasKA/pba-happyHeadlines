using HappyHeadlines.Shared;
using StackExchange.Redis;

namespace HappyHeadlines.CommentService.Infrastructure;

public sealed class CommentCache(IConnectionMultiplexer redis, IConfiguration configuration)
{
    private const string EnforceLruLimit = """
        redis.call('ZADD', KEYS[1], ARGV[1], ARGV[2])
        local count = redis.call('ZCARD', KEYS[1])
        local removeCount = count - 30
        local removed = {}
        if removeCount > 0 then
          removed = redis.call('ZRANGE', KEYS[1], 0, removeCount - 1)
          for _, articleId in ipairs(removed) do
            redis.call('DEL', ARGV[3] .. articleId)
          end
          redis.call('ZREMRANGEBYRANK', KEYS[1], 0, removeCount - 1)
        end
        return removed
        """;

    private readonly IDatabase _database = redis.GetDatabase();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(15);
    private readonly bool _enabled = configuration.GetValue("Caching:CommentEnabled", true);

    public async Task<(IReadOnlyList<CommentRecord>? Value, bool Available)> GetAsync(
        Guid articleId, string region, CancellationToken cancellationToken, ILogger logger)
    {
        region = Regions.Normalize(region);
        if (!_enabled)
        {
            HappyMetrics.CacheBypasses.WithLabels("comment", region).Inc();
            return (null, true);
        }

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        using var timedOperation = logger.BeginTimedOperation("comment-cache-get");
        try
        {
            var value = await _database.StringGetAsync(CacheKeys.Comments(articleId, region));
            if (value.HasValue)
            {
                HappyMetrics.CacheHits.WithLabels("comment", region).Inc();
                await TouchAsync(articleId, region);
                return (System.Text.Json.JsonSerializer.Deserialize<List<CommentRecord>>(value.ToString(), JsonDefaults.Options) ?? [], true);
            }

            HappyMetrics.CacheMisses.WithLabels("comment", region).Inc();
            return (null, true);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            HappyMetrics.CacheFallbacks.WithLabels("comment", region).Inc();
            using (logger.Here())
                logger.LogWarning(exception, "Comment cache unavailable; falling back to PostgreSQL for article {ArticleId}", articleId);
            return (null, false);
        }
        finally
        {
            HappyMetrics.CacheOperationDuration.WithLabels("comment", "get")
                .Observe(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds);
        }
    }

    public async Task SetAsync(Guid articleId, string region, IReadOnlyList<CommentRecord> comments,
        CancellationToken cancellationToken, ILogger logger)
    {
        region = Regions.Normalize(region);
        if (!_enabled)
            return;

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        using var timedOperation = logger.BeginTimedOperation("comment-cache-set");
        try
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(comments, JsonDefaults.Options);
            await _database.StringSetAsync(CacheKeys.Comments(articleId, region), payload, _ttl);
            await TouchAsync(articleId, region);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            HappyMetrics.CacheFallbacks.WithLabels("comment", region).Inc();
            using (logger.Here())
                logger.LogWarning(exception, "Unable to populate comment cache for article {ArticleId}", articleId);
        }
        finally
        {
            HappyMetrics.CacheOperationDuration.WithLabels("comment", "set")
                .Observe(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds);
        }
    }

    public async Task InvalidateAsync(Guid articleId, string region, CancellationToken cancellationToken, ILogger logger)
    {
        region = Regions.Normalize(region);
        if (!_enabled)
            return;

        try
        {
            await _database.KeyDeleteAsync(CacheKeys.Comments(articleId, region));
            await _database.SortedSetRemoveAsync(CacheKeys.CommentLru(region), articleId.ToString());
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            HappyMetrics.CacheFallbacks.WithLabels("comment", region).Inc();
            using (logger.Here())
                logger.LogWarning(exception, "Unable to invalidate comment cache for article {ArticleId}", articleId);
        }
    }

    private Task TouchAsync(Guid articleId, string region) => _database.ScriptEvaluateAsync(
        EnforceLruLimit,
        new RedisKey[] { CacheKeys.CommentLru(region) },
        new RedisValue[]
        {
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            articleId.ToString(),
            $"comments:{region}:"
        });
}
