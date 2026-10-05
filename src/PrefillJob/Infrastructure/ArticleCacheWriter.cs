using HappyHeadlines.Shared;
using StackExchange.Redis;

namespace HappyHeadlines.PrefillJob.Infrastructure;

public sealed class ArticleCacheWriter(IConnectionMultiplexer redis) : IArticleCacheWriter
{
    private readonly IDatabase _database = redis.GetDatabase();

    public Task WriteAsync(ArticleRecord article, TimeSpan ttl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = System.Text.Json.JsonSerializer.Serialize(article, JsonDefaults.Options);
        return _database.StringSetAsync(CacheKeys.Article(article.Id, article.Region), json, ttl);
    }
}
