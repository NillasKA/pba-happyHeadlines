using HappyHeadlines.Shared;

namespace HappyHeadlines.ArticleService.Services;

public interface IArticleService
{
    Task<IReadOnlyList<ArticleRecord>> GetRecentAsync(string? region, CancellationToken cancellationToken);
    Task<ArticleRecord?> GetAsync(Guid id, string? region, CancellationToken cancellationToken);
    Task PersistPublishedAsync(ArticlePublished article, CancellationToken cancellationToken);
}
