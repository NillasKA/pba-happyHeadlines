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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await prefillService.PrefillRecentArticlesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                using (logger.Here())
                    logger.LogError(exception, "Article cache prefill failed; the job will retry next interval");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
