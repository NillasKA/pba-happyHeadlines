using HappyHeadlines.Shared;
using Npgsql;

namespace HappyHeadlines.ArticleService.Infrastructure;

public sealed class ArticleRepository(NpgsqlDataSource dataSource)
{
    public async Task<ArticleRecord?> GetAsync(Guid id, string region, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT id, title, body, region, published_at FROM articles WHERE id = @id AND region = @region");
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("region", Regions.Normalize(region));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadArticle(reader) : null;
    }

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
            articles.Add(ReadArticle(reader));
        return articles;
    }

    public async Task UpsertAsync(ArticlePublished article, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO articles (region, id, title, body, published_at)
            VALUES (@region, @id, @title, @body, @published_at)
            ON CONFLICT (region, id) DO UPDATE
            SET title = EXCLUDED.title, body = EXCLUDED.body, published_at = EXCLUDED.published_at
            """);
        command.Parameters.AddWithValue("region", Regions.Normalize(article.Region));
        command.Parameters.AddWithValue("id", article.Id);
        command.Parameters.AddWithValue("title", article.Title);
        command.Parameters.AddWithValue("body", article.Body);
        command.Parameters.AddWithValue("published_at", article.PublishedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ArticleRecord ReadArticle(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
        reader.GetFieldValue<DateTimeOffset>(4));
}
