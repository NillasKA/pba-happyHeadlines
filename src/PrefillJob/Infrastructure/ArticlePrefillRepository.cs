using HappyHeadlines.Shared;
using Npgsql;

namespace HappyHeadlines.PrefillJob.Infrastructure;

public sealed class ArticlePrefillRepository(NpgsqlDataSource dataSource) : IArticlePrefillRepository
{
    public async Task<IReadOnlyList<ArticleRecord>> GetRecentAsync(
        string region, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT id, title, body, region, published_at FROM articles WHERE region = @region AND published_at >= @cutoff ORDER BY published_at DESC");
        command.Parameters.AddWithValue("region", Regions.Normalize(region));
        command.Parameters.AddWithValue("cutoff", cutoff);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var articles = new List<ArticleRecord>();
        while (await reader.ReadAsync(cancellationToken))
            articles.Add(new ArticleRecord(reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc))));
        return articles;
    }
}
