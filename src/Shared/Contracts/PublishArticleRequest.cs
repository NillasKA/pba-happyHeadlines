namespace HappyHeadlines.Shared;

public sealed record PublishArticleRequest(
    string Title,
    string Body,
    string? Region = null);
