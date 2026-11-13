using HappyHeadlines.NewsletterService.Infrastructure;
using HappyHeadlines.Shared;

namespace HappyHeadlines.NewsletterService.Workers;

public sealed class OutboxDispatcher(ISubscriberOutbox outbox, ISubscriberPublisher publisher,
    ISubscriberFeature feature, ILogger<OutboxDispatcher> logger)
{
    public async Task DispatchAsync(CancellationToken cancellationToken)
    {
        if (!await feature.IsEnabledAsync(cancellationToken)) return;
        foreach (var message in await outbox.PendingAsync(cancellationToken))
        {
            if (!await feature.IsEnabledAsync(cancellationToken)) return;
            var article = message.Article;
            System.Diagnostics.ActivityContext.TryParse(message.TraceParent, message.TraceState, out var parent);
            using var activity = TraceContext.Source.StartActivity("subscriber-outbox.publish", System.Diagnostics.ActivityKind.Producer, parent);
            activity?.SetTag("article.id", article.Id);
            await publisher.PublishAsync(article, cancellationToken);
            await outbox.MarkSentAsync(article.Id, cancellationToken);
            HappyMetrics.SubscriberEvents.WithLabels("queued").Inc();
            logger.LogInformation("Queued subscriber work for article {ArticleId}", article.Id);
        }
    }
}

public sealed class SubscriberOutboxWorker(OutboxDispatcher dispatcher, ILogger<SubscriberOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await dispatcher.DispatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                HappyMetrics.SubscriberEvents.WithLabels("retry").Inc();
                logger.LogWarning(exception, "Subscriber outbox dispatch failed; retrying");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
