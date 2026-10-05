namespace HappyHeadlines.Shared;

public sealed record ArticleRecord(
    Guid Id,
    string Title,
    string Body,
    string Region,
    DateTimeOffset PublishedAt);
