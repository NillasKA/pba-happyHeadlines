using HappyHeadlines.Shared;

namespace HappyHeadlines.PublisherService.Services;

public interface IPublisherService
{
    Task<ArticlePublished> PublishAsync(PublishArticleRequest request, CancellationToken cancellationToken);
}
