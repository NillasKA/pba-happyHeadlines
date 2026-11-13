using System.Text.Json;
using HappyHeadlines.Shared;
using Npgsql;

namespace HappyHeadlines.NewsletterService.Infrastructure;

public sealed record OutboxMessage(ArticlePublished Article, string? TraceParent, string? TraceState);

public interface ISubscriberOutbox
{
    Task EnqueueAsync(ArticlePublished article, CancellationToken cancellationToken);
    Task<IReadOnlyList<OutboxMessage>> PendingAsync(CancellationToken cancellationToken);
    Task MarkSentAsync(Guid articleId, CancellationToken cancellationToken);
}

public sealed class SubscriberOutbox(NpgsqlDataSource dataSource) : ISubscriberOutbox
{
    private async Task EnsureSchemaAsync(CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("""
            CREATE TABLE IF NOT EXISTS subscriber_outbox (
                article_id uuid PRIMARY KEY, payload jsonb NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now(), sent_at timestamptz NULL, trace_parent text NULL, trace_state text NULL);
            """);
        await command.ExecuteNonQueryAsync(token);
    }
    public async Task EnqueueAsync(ArticlePublished article, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            INSERT INTO subscriber_outbox (article_id, payload, trace_parent, trace_state) VALUES (@id, CAST(@payload AS jsonb), @trace_parent, @trace_state)
            ON CONFLICT (article_id) DO NOTHING
            """);
        command.Parameters.AddWithValue("id", article.Id);
        command.Parameters.AddWithValue("payload", JsonSerializer.Serialize(article, JsonDefaults.Options));
        command.Parameters.AddWithValue("trace_parent", NpgsqlTypes.NpgsqlDbType.Text, (object?)System.Diagnostics.Activity.Current?.Id ?? DBNull.Value);
        command.Parameters.AddWithValue("trace_state", NpgsqlTypes.NpgsqlDbType.Text, (object?)System.Diagnostics.Activity.Current?.TraceStateString ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<OutboxMessage>> PendingAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("SELECT payload::text, trace_parent, trace_state FROM subscriber_outbox WHERE sent_at IS NULL ORDER BY created_at, article_id LIMIT 100");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var articles = new List<OutboxMessage>();
        while (await reader.ReadAsync(cancellationToken))
            articles.Add(new OutboxMessage(JsonSerializer.Deserialize<ArticlePublished>(reader.GetString(0), JsonDefaults.Options)
                ?? throw new InvalidOperationException("Outbox payload is empty."),
                reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        return articles;
    }
    public async Task MarkSentAsync(Guid articleId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("UPDATE subscriber_outbox SET sent_at = now() WHERE article_id = @id AND sent_at IS NULL");
        command.Parameters.AddWithValue("id", articleId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
