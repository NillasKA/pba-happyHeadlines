using System.Net.Http.Json;
using HappyHeadlines.Shared;

namespace HappyHeadlines.CommentService.Services;

public sealed class ModerationUnavailableException : Exception
{
    public ModerationUnavailableException() : base("Comment moderation is temporarily unavailable.") { }
}

public sealed class CommentRejectedException : Exception
{
    public CommentRejectedException() : base("Comment contains prohibited language.") { }
}

public interface IProfanityClient
{
    Task CheckAsync(string body, CancellationToken cancellationToken);
}

public sealed class ProfanityClient : IProfanityClient
{
    private readonly HttpClient client;
    private readonly ILogger<ProfanityClient> logger;
    private readonly TimeProvider clock;
    private readonly TimeSpan breakDuration;
    public ProfanityClient(HttpClient client, ILogger<ProfanityClient> logger) : this(client, logger, TimeProvider.System, TimeSpan.FromSeconds(15)) { }
    public ProfanityClient(HttpClient client, ILogger<ProfanityClient> logger, TimeProvider clock, TimeSpan breakDuration)
    {
        this.client = client;
        this.logger = logger;
        this.clock = clock;
        this.breakDuration = breakDuration;
    }
    private readonly object gate = new();
    private int failures;
    private long generation;
    private DateTimeOffset openUntil;
    private bool probing;
    private const int FailureThreshold = 3;


    public async Task CheckAsync(string body, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool probe;
        long requestGeneration;
        lock (gate)
        {
            if (openUntil > clock.GetUtcNow() || (openUntil != default && probing))
                throw new ModerationUnavailableException();
            requestGeneration = generation;
            probe = openUntil != default;
            if (probe) probing = true;
        }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            using var response = await client.PostAsJsonAsync("/moderation/check", new ModerationRequest(body), timeout.Token);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ModerationResult>(cancellationToken: timeout.Token)
                ?? throw new InvalidOperationException("Empty moderation response.");
            lock (gate) { if (requestGeneration == generation) { failures = 0; openUntil = default; probing = false; } }
            HappyMetrics.ModerationChecks.WithLabels(result.Allowed ? "allowed" : "rejected").Inc();
            if (!result.Allowed) throw new CommentRejectedException();
        }
        catch (CommentRejectedException) { throw; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (gate) { if (probe && requestGeneration == generation) probing = false; }
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or InvalidOperationException or System.Text.Json.JsonException)
        {
            lock (gate)
            {
                if (requestGeneration == generation)
                {
                    failures++;
                    if (probe || failures >= FailureThreshold)
                    {
                        openUntil = clock.GetUtcNow().Add(breakDuration);
                        generation++;
                    }
                    probing = false;
                }
            }
            HappyMetrics.ModerationChecks.WithLabels("unavailable").Inc();
            logger.LogWarning(exception, "Moderation request failed; circuit may be open");
            throw new ModerationUnavailableException();
        }
    }
}
