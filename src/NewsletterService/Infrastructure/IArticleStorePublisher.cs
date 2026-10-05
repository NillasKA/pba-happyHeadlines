using HappyHeadlines.Shared;

namespace HappyHeadlines.NewsletterService.Infrastructure;

public interface IArticleStorePublisher
{
    Task PublishAsync(ArticlePublished article, CancellationToken cancellationToken);
}
