using HappyHeadlines.Shared;

namespace HappyHeadlines.PublisherService.Infrastructure;

public interface IArticleEventPublisher
{
    Task PublishAsync(ArticlePublished article, CancellationToken cancellationToken);
}
