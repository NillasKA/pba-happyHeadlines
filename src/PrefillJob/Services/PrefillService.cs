using HappyHeadlines.PrefillJob.Infrastructure;
using HappyHeadlines.Shared;

namespace HappyHeadlines.PrefillJob.Services;

public sealed class PrefillService(
    IArticlePrefillRepository repository,
    IArticleCacheWriter cacheWriter,
    IConfiguration configuration,
    ILogger<PrefillService> logger) : IPrefillService
{
    public async Task<int> PrefillRecentArticlesAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-14);
        var ttl = TimeSpan.FromHours(configuration.GetValue("Prefill:ArticleTtlHours", 8));
        var total = 0;

        foreach (var region in new[] { "eu", "na" })
        {
            var articles = await repository.GetRecentAsync(region, cutoff, cancellationToken);
            foreach (var article in articles)
                await cacheWriter.WriteAsync(article, ttl, cancellationToken);

            total += articles.Count;
            using (logger.Here())
                logger.LogInformation("Prefilled {Count} articles published in the last 14 days for {Region}",
                    articles.Count, region);
        }

        return total;
    }
}
