using HappyHeadlines.Shared;

namespace HappyHeadlines.PrefillJob.Infrastructure;

public interface IArticleCacheWriter
{
    Task WriteAsync(ArticleRecord article, TimeSpan ttl, CancellationToken cancellationToken);
}
