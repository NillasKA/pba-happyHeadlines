using System.Net;
using System.Net.Http.Json;
using HappyHeadlines.CommentService.Services;
using HappyHeadlines.NewsletterService.Infrastructure;
using HappyHeadlines.NewsletterService.Services;
using HappyHeadlines.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ServiceTests;

public sealed class ResilienceTests
{
    [Fact]
    public void Moderation_registration_resolves_one_shared_breaker()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProfanityModeration(new Uri("http://moderation.test"));
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IProfanityClient>();
        var second = secondScope.ServiceProvider.GetRequiredService<IProfanityClient>();
        Assert.IsType<ProfanityClient>(first);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task Breaker_opens_and_recovers_after_one_probe()
    {
        var clock = new TestClock();
        var handler = new Handler();
        var client = new ProfanityClient(new HttpClient(handler) { BaseAddress = new Uri("http://test") },
            NullLogger<ProfanityClient>.Instance, clock, TimeSpan.FromSeconds(15));
        for (var i = 0; i < 3; i++) await Assert.ThrowsAsync<ModerationUnavailableException>(() => client.CheckAsync("hello", default));
        Assert.Equal(3, handler.Calls);
        await Assert.ThrowsAsync<ModerationUnavailableException>(() => client.CheckAsync("hello", default));
        Assert.Equal(3, handler.Calls);
        clock.Advance(TimeSpan.FromSeconds(16));
        handler.Success = true;
        await client.CheckAsync("hello", default);
        await client.CheckAsync("hello", default);
        Assert.Equal(5, handler.Calls);
    }

    [Fact]
    public async Task Half_open_allows_only_one_probe_and_failed_probe_reopens()
    {
        var clock = new TestClock();
        var handler = new ProbeHandler();
        var client = new ProfanityClient(new HttpClient(handler) { BaseAddress = new Uri("http://test") },
            NullLogger<ProfanityClient>.Instance, clock, TimeSpan.FromSeconds(15));
        for (var i = 0; i < 3; i++) await Assert.ThrowsAsync<ModerationUnavailableException>(() => client.CheckAsync("hello", default));
        clock.Advance(TimeSpan.FromSeconds(16));
        var probe = client.CheckAsync("hello", default);
        await handler.Started.Task;
        await Assert.ThrowsAsync<ModerationUnavailableException>(() => client.CheckAsync("hello", default));
        Assert.Equal(4, handler.Calls);
        handler.Release.SetResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        await Assert.ThrowsAsync<ModerationUnavailableException>(() => probe);
        await Assert.ThrowsAsync<ModerationUnavailableException>(() => client.CheckAsync("hello", default));
        Assert.Equal(4, handler.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_does_not_open_circuit()
    {
        var handler = new Handler();
        var client = new ProfanityClient(new HttpClient(handler) { BaseAddress = new Uri("http://test") }, NullLogger<ProfanityClient>.Instance);
        using var source = new CancellationTokenSource(); source.Cancel();
        for (var i = 0; i < 4; i++) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CheckAsync("hello", source.Token));
        handler.Success = true;
        await client.CheckAsync("hello", default);
        Assert.Equal(1, handler.Calls);
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
    private sealed class ProbeHandler : HttpMessageHandler
    {
        public int Calls;
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<HttpResponseMessage> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls <= 3) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            Started.SetResult();
            return Release.Task;
        }
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls; public bool Success;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Success ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ModerationResult(true)) }
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }

}
