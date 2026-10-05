using HappyHeadlines.Shared;
using Npgsql;

namespace HappyHeadlines.CommentService.Infrastructure;

public sealed class CommentRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<CommentRecord>> GetForArticleAsync(
        Guid articleId, string region, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT id, article_id, body, region, created_at FROM comments WHERE article_id = @article_id AND region = @region ORDER BY created_at");
        command.Parameters.AddWithValue("article_id", articleId);
        command.Parameters.AddWithValue("region", Regions.Normalize(region));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var comments = new List<CommentRecord>();
        while (await reader.ReadAsync(cancellationToken))
            comments.Add(new CommentRecord(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2),
                reader.GetString(3), new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc))));
        return comments;
    }

    public async Task InsertAsync(CommentRecord comment, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "INSERT INTO comments (region, id, article_id, body, created_at) VALUES (@region, @id, @article_id, @body, @created_at)");
        command.Parameters.AddWithValue("region", comment.Region);
        command.Parameters.AddWithValue("id", comment.Id);
        command.Parameters.AddWithValue("article_id", comment.ArticleId);
        command.Parameters.AddWithValue("body", comment.Body);
        command.Parameters.AddWithValue("created_at", comment.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
