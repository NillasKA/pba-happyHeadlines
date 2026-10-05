using HappyHeadlines.Shared;

namespace HappyHeadlines.NewsletterService.Services;

public interface INewsletterService
{
    Task ProcessAsync(ArticlePublished article, CancellationToken cancellationToken);
}
