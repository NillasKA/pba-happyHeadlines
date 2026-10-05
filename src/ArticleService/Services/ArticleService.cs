using HappyHeadlines.ArticleService.Infrastructure;
using HappyHeadlines.Shared;

namespace HappyHeadlines.ArticleService.Services;

public sealed class ArticleService(
    ArticleRepository repository,
    ArticleCache cache,
    StripedAsyncLock missLock,
    ILogger<ArticleService> logger) : IArticleService
{
    public Task<IReadOnlyList<ArticleRecord>> GetRecentAsync(string? region, CancellationToken cancellationToken) =>
        repository.GetRecentAsync(Regions.Normalize(region), DateTimeOffset.UtcNow.AddDays(-14), cancellationToken);

    public async Task<ArticleRecord?> GetAsync(Guid id, string? requestedRegion, CancellationToken cancellationToken)
    {
        var region = Regions.Normalize(requestedRegion);
        var cached = await cache.GetAsync(id, region, cancellationToken, logger);
        if (cached.Value is not null)
            return cached.Value;
        if (!cached.Available)
            return await repository.GetAsync(id, region, cancellationToken);

        using (await missLock.LockAsync($"article:{region}:{id}", cancellationToken))
        {
            // Another request in this process may have populated Redis while we waited.
            cached = await cache.GetAsync(id, region, cancellationToken, logger);
            if (cached.Value is not null)
                return cached.Value;
            if (!cached.Available)
                return await repository.GetAsync(id, region, cancellationToken);

            var article = await repository.GetAsync(id, region, cancellationToken);
            if (article is not null)
                await cache.SetAsync(article, cancellationToken, logger);
            return article;
        }
    }

    public async Task PersistPublishedAsync(ArticlePublished article, CancellationToken cancellationToken)
    {
        article = article with { Region = Regions.Normalize(article.Region) };
        await repository.UpsertAsync(article, cancellationToken);
        await cache.RemoveAsync(article.Id, article.Region, cancellationToken, logger);
        using (logger.Here())
            logger.LogInformation("Persisted queued article {ArticleId} in {Region}", article.Id, article.Region);
    }
}
