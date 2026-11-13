using System.Text;
using HappyHeadlines.NewsletterService.Infrastructure;
using HappyHeadlines.NewsletterService.Workers;
using HappyHeadlines.PrefillJob.Infrastructure;
using HappyHeadlines.PrefillJob.Services;
using HappyHeadlines.Shared;
using HappyHeadlines.SubscriberService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ServiceTests;

public sealed class SubscriberWorkflowTests
{
    private static ArticlePublished Article() => new(Guid.NewGuid(), "Title", "Body", "eu", DateTimeOffset.UtcNow);

    [Fact]
    public async Task Disabled_feature_preserves_article_storage_and_reenabled_outbox_retries_without_duplicates()
    {
        var feature = new Feature(); var outbox = new Outbox(); var store = new Store(); var publisher = new Publisher();
        var service = new HappyHeadlines.NewsletterService.Services.NewsletterService(store, outbox, feature,
            NullLogger<HappyHeadlines.NewsletterService.Services.NewsletterService>.Instance);
        var dispatcher = new OutboxDispatcher(outbox, publisher, feature, NullLogger<OutboxDispatcher>.Instance);
        var article = Article();
        await service.ProcessAsync(article, default);
        Assert.Equal(1, store.Calls); Assert.Empty(outbox.Messages);
        feature.Enabled = true;
        await service.ProcessAsync(article, default);
        await service.ProcessAsync(article, default);
        Assert.Single(outbox.Messages);
        publisher.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(default));
        Assert.Single(await outbox.PendingAsync(default));
        feature.Enabled = false;
        publisher.Fail = false;
        await dispatcher.DispatchAsync(default);
        Assert.Equal(1, publisher.Calls);
        feature.Enabled = true;
        await dispatcher.DispatchAsync(default);
        await dispatcher.DispatchAsync(default);
        Assert.Equal(2, publisher.Calls);
        Assert.Empty(await outbox.PendingAsync(default));
        await service.ProcessAsync(article, default);
        Assert.Empty(await outbox.PendingAsync(default));
        Assert.Equal(4, store.Calls);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"id\":\"invalid\"}")]
    public void Malformed_events_are_rejected(string json) =>
        Assert.False(SubscriberEventParser.TryParse(Encoding.UTF8.GetBytes(json), out _));

    [Fact]
    public void Valid_events_are_accepted()
    {
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Article(), JsonDefaults.Options);
        Assert.True(SubscriberEventParser.TryParse(bytes, out _));
    }

    [Fact]
    public async Task Prefill_continues_after_one_region_fails()
    {
        var repo = new PrefillRepository(); var cache = new Cache();
        var service = new PrefillService(repo, cache, new ConfigurationBuilder().Build(), NullLogger<PrefillService>.Instance);
        var result = await service.PrefillRecentArticlesAsync(default);
        Assert.Equal(1, result.Count);
        Assert.Equal(1, result.FailedRegions);
        Assert.Equal(new[] { "eu", "na" }, repo.Regions);
        Assert.Equal(1, cache.Calls);
    }

    private sealed class Feature : ISubscriberFeature
    {
        public bool Enabled;
        public Task<bool> IsEnabledAsync(CancellationToken token) => Task.FromResult(Enabled);
        public Task SetEnabledAsync(bool enabled, CancellationToken token) { Enabled = enabled; return Task.CompletedTask; }
    }
    private sealed class Store : IArticleStorePublisher
    {
        public int Calls;
        public Task PublishAsync(ArticlePublished article, CancellationToken token) { Calls++; return Task.CompletedTask; }
    }
    private sealed class Publisher : ISubscriberPublisher
    {
        public int Calls; public bool Fail;
        public Task PublishAsync(ArticlePublished article, CancellationToken token)
        { Calls++; if (Fail) throw new InvalidOperationException(); return Task.CompletedTask; }
    }
    private sealed class Outbox : ISubscriberOutbox
    {
        public Dictionary<Guid, (OutboxMessage Message, bool Sent)> Messages = new();
        public Task EnqueueAsync(ArticlePublished article, CancellationToken token)
        { Messages.TryAdd(article.Id, (new OutboxMessage(article, null, null), false)); return Task.CompletedTask; }
        public Task<IReadOnlyList<OutboxMessage>> PendingAsync(CancellationToken token) =>
            Task.FromResult<IReadOnlyList<OutboxMessage>>(Messages.Values.Where(x => !x.Sent).Select(x => x.Message).ToList());
        public Task MarkSentAsync(Guid id, CancellationToken token)
        { Messages[id] = (Messages[id].Message, true); return Task.CompletedTask; }
    }
    private sealed class PrefillRepository : IArticlePrefillRepository
    {
        public List<string> Regions = new();
        public Task<IReadOnlyList<ArticleRecord>> GetRecentAsync(string region, DateTimeOffset cutoff, CancellationToken token)
        {
            Regions.Add(region);
            if (region == "eu") throw new InvalidOperationException();
            return Task.FromResult<IReadOnlyList<ArticleRecord>>(new[] { new ArticleRecord(Guid.NewGuid(), "t", "b", "na", DateTimeOffset.UtcNow) });
        }
    }
    private sealed class Cache : IArticleCacheWriter
    {
        public int Calls;
        public Task WriteAsync(ArticleRecord article, TimeSpan ttl, CancellationToken token) { Calls++; return Task.CompletedTask; }
    }
}
