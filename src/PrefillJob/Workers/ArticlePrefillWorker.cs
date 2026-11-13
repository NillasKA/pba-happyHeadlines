using HappyHeadlines.PrefillJob.Services;
using HappyHeadlines.Shared;

namespace HappyHeadlines.PrefillJob.Workers;

public sealed class ArticlePrefillWorker(
    IPrefillService prefillService,
    IConfiguration configuration,
    ILogger<ArticlePrefillWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(configuration.GetValue("Prefill:IntervalMinutes", 360));
        var retry = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Prefill:RetrySeconds", 60), 1, 300));
        while (!stoppingToken.IsCancellationRequested)
        {
            var nextDelay = retry;
            try
            {
                var result = await prefillService.PrefillRecentArticlesAsync(stoppingToken);
                if (result.FailedRegions == 0) nextDelay = interval;
                else logger.LogWarning("Prefill failed for {Count} regions; retrying in {Delay}", result.FailedRegions, retry);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                using (logger.Here())
                    logger.LogError(exception, "Article cache prefill failed; the job will retry shortly");
            }

            try { await Task.Delay(nextDelay, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
