using HappyHeadlines.PublisherService.Infrastructure;
using HappyHeadlines.Shared;

namespace HappyHeadlines.PublisherService.Services;

public sealed class PublisherService(IArticleEventPublisher eventPublisher) : IPublisherService
{
    public async Task<ArticlePublished> PublishAsync(PublishArticleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            throw new ArgumentException("title and body are required");

        var article = new ArticlePublished(Guid.NewGuid(), request.Title.Trim(), request.Body.Trim(),
            Regions.Normalize(request.Region), DateTimeOffset.UtcNow);
        await eventPublisher.PublishAsync(article, cancellationToken);
        return article;
    }
}
