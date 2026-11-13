using HappyHeadlines.PrefillJob.Infrastructure;
using HappyHeadlines.Shared;

namespace HappyHeadlines.PrefillJob.Services;

public sealed class PrefillService(
    IArticlePrefillRepository repository,
    IArticleCacheWriter cacheWriter,
    IConfiguration configuration,
    ILogger<PrefillService> logger) : IPrefillService
{
    public async Task<PrefillResult> PrefillRecentArticlesAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-14);
        var ttl = TimeSpan.FromHours(configuration.GetValue("Prefill:ArticleTtlHours", 8));
        var total = 0;
        var failedRegions = 0;

        foreach (var region in new[] { "eu", "na" })
        {
            try
            {
                var articles = await repository.GetRecentAsync(region, cutoff, cancellationToken);
                foreach (var batch in articles.Chunk(100))
                    await Task.WhenAll(batch.Select(article => cacheWriter.WriteAsync(article, ttl, cancellationToken)));

                total += articles.Count;
                using (logger.Here())
                    logger.LogInformation("Prefilled {Count} articles published in the last 14 days for {Region}",
                        articles.Count, region);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                failedRegions++;
                logger.LogWarning(exception, "Prefill failed for {Region}; continuing with other regions", region);
            }
        }

        return new PrefillResult(total, failedRegions);
    }
}
