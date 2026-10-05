using HappyHeadlines.Shared;

namespace HappyHeadlines.PrefillJob.Infrastructure;

public interface IArticlePrefillRepository
{
    Task<IReadOnlyList<ArticleRecord>> GetRecentAsync(string region, DateTimeOffset cutoff, CancellationToken cancellationToken);
}
