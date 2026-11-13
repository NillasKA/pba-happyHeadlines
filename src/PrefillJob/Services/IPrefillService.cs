namespace HappyHeadlines.PrefillJob.Services;

public sealed record PrefillResult(int Count, int FailedRegions);

public interface IPrefillService
{
    Task<PrefillResult> PrefillRecentArticlesAsync(CancellationToken cancellationToken);
}
