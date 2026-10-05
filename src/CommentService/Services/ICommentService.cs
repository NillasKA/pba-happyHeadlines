using HappyHeadlines.Shared;

namespace HappyHeadlines.CommentService.Services;

public interface ICommentService
{
    Task<IReadOnlyList<CommentRecord>> GetForArticleAsync(Guid articleId, string? region, CancellationToken cancellationToken);
    Task<CommentRecord> CreateAsync(Guid articleId, CreateCommentRequest request, string? region, CancellationToken cancellationToken);
}
