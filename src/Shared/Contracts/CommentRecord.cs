namespace HappyHeadlines.Shared;

public sealed record CommentRecord(
    Guid Id,
    Guid ArticleId,
    string Body,
    string Region,
    DateTimeOffset CreatedAt);
