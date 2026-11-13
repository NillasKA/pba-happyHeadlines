using Npgsql;

namespace HappyHeadlines.SubscriberService;

public sealed record Subscriber(Guid Id, string Email);

public sealed class SubscriberRepository(NpgsqlDataSource dataSource)
{
    public async Task EnsureSchemaAsync(CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("""
            CREATE TABLE IF NOT EXISTS subscribers (id uuid PRIMARY KEY, email text NOT NULL UNIQUE);
            CREATE TABLE IF NOT EXISTS processed_subscriber_articles (article_id uuid PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS subscriber_notifications (
                article_id uuid NOT NULL, subscriber_id uuid NOT NULL REFERENCES subscribers(id),
                created_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY (article_id, subscriber_id));
            """);
        await command.ExecuteNonQueryAsync(token);
    }

    public async Task<Subscriber> RegisterAsync(string email, CancellationToken token)
    {
        await EnsureSchemaAsync(token);
        await using var command = dataSource.CreateCommand("""
            INSERT INTO subscribers (id, email) VALUES (@id, @email)
            ON CONFLICT (email) DO UPDATE SET email = EXCLUDED.email RETURNING id, email
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("email", email);
        await using var reader = await command.ExecuteReaderAsync(token);
        await reader.ReadAsync(token);
        return new Subscriber(reader.GetGuid(0), reader.GetString(1));
    }

    public async Task<List<Subscriber>> ListAsync(CancellationToken token)
    {
        await EnsureSchemaAsync(token);
        await using var command = dataSource.CreateCommand("SELECT id, email FROM subscribers ORDER BY email");
        await using var reader = await command.ExecuteReaderAsync(token);
        var subscribers = new List<Subscriber>();
        while (await reader.ReadAsync(token)) subscribers.Add(new Subscriber(reader.GetGuid(0), reader.GetString(1)));
        return subscribers;
    }

    public async Task<int> RecordNotificationsAsync(Guid articleId, CancellationToken token)
    {
        await EnsureSchemaAsync(token);
        await using var command = dataSource.CreateCommand("""
            WITH claimed AS (
                INSERT INTO processed_subscriber_articles (article_id) VALUES (@article_id)
                ON CONFLICT DO NOTHING RETURNING article_id
            )
            INSERT INTO subscriber_notifications (article_id, subscriber_id)
            SELECT claimed.article_id, subscribers.id FROM claimed CROSS JOIN subscribers
            ON CONFLICT DO NOTHING
            """);
        command.Parameters.AddWithValue("article_id", articleId);
        return await command.ExecuteNonQueryAsync(token);
    }
}
