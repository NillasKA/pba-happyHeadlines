namespace HappyHeadlines.Shared;

public sealed record ModerationRequest(string Body);
public sealed record ModerationResult(bool Allowed);
