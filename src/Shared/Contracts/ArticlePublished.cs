namespace HappyHeadlines.Shared;

public sealed record ArticlePublished(
    Guid Id,
    string Title,
    string Body,
    string Region,
    DateTimeOffset PublishedAt);
