namespace HappyHeadlines.PrefillJob.Services;

public interface IPrefillService
{
    Task<int> PrefillRecentArticlesAsync(CancellationToken cancellationToken);
}
