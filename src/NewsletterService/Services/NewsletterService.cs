using HappyHeadlines.NewsletterService.Infrastructure;
using HappyHeadlines.Shared;

namespace HappyHeadlines.NewsletterService.Services;

public sealed class NewsletterService(
    IArticleStorePublisher articleStorePublisher,
    ILogger<NewsletterService> logger) : INewsletterService
{
    public async Task ProcessAsync(ArticlePublished article, CancellationToken cancellationToken)
    {
        article = article with { Region = Regions.Normalize(article.Region) };
        await articleStorePublisher.PublishAsync(article, cancellationToken);
        HappyMetrics.ConsumedEvents.Inc();
        using (logger.Here())
            logger.LogInformation("Newsletter workflow processed article {ArticleId} for region {Region}",
                article.Id, article.Region);
    }
}
