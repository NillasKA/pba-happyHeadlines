using System.Text.Json;
using HappyHeadlines.Shared;
using System.Diagnostics.CodeAnalysis;

namespace HappyHeadlines.SubscriberService;

public static class SubscriberEventParser
{
    public static bool TryParse(ReadOnlyMemory<byte> body, [NotNullWhen(true)] out ArticlePublished? article)
    {
        try
        {
            article = JsonSerializer.Deserialize<ArticlePublished>(body.Span, JsonDefaults.Options);
            return article is not null && article.Id != Guid.Empty && !string.IsNullOrWhiteSpace(article.Title)
                && !string.IsNullOrWhiteSpace(article.Body) && !string.IsNullOrWhiteSpace(article.Region)
                && article.PublishedAt != default;
        }
        catch (JsonException) { article = null; return false; }
        catch (NotSupportedException) { article = null; return false; }
    }
}
