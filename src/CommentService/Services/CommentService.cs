using HappyHeadlines.CommentService.Infrastructure;
using HappyHeadlines.Shared;

namespace HappyHeadlines.CommentService.Services;

public sealed class CommentService(
    CommentRepository repository,
    CommentCache cache,
    StripedAsyncLock missLock,
    ILogger<CommentService> logger) : ICommentService
{
    public async Task<IReadOnlyList<CommentRecord>> GetForArticleAsync(
        Guid articleId, string? requestedRegion, CancellationToken cancellationToken)
    {
        var region = Regions.Normalize(requestedRegion);
        var cached = await cache.GetAsync(articleId, region, cancellationToken, logger);
        if (cached.Value is not null)
            return cached.Value;
        if (!cached.Available)
            return await repository.GetForArticleAsync(articleId, region, cancellationToken);

        using (await missLock.LockAsync($"comments:{region}:{articleId}", cancellationToken))
        {
            // Another request in this process may have populated Redis while we waited.
            cached = await cache.GetAsync(articleId, region, cancellationToken, logger);
            if (cached.Value is not null)
                return cached.Value;
            if (!cached.Available)
                return await repository.GetForArticleAsync(articleId, region, cancellationToken);

            var comments = await repository.GetForArticleAsync(articleId, region, cancellationToken);
            await cache.SetAsync(articleId, region, comments, cancellationToken, logger);
            return comments;
        }
    }

    public async Task<CommentRecord> CreateAsync(
        Guid articleId, CreateCommentRequest request, string? requestedRegion, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new ArgumentException("comment body is required");

        var region = Regions.Normalize(requestedRegion);
        var comment = new CommentRecord(Guid.NewGuid(), articleId, request.Body.Trim(), region, DateTimeOffset.UtcNow);
        await repository.InsertAsync(comment, cancellationToken);
        await cache.InvalidateAsync(articleId, region, cancellationToken, logger);
        return comment;
    }
}
