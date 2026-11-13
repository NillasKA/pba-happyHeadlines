using Prometheus;

namespace HappyHeadlines.Shared;

public static class HappyMetrics
{
    public static readonly Counter HttpRequests = Metrics.CreateCounter(
        "happy_headlines_http_requests_total",
        "HTTP requests served by Happy Headlines services.",
        new[] { "service", "region", "method", "route", "status" });

    public static readonly Histogram HttpDuration = Metrics.CreateHistogram(
        "happy_headlines_http_request_duration_seconds",
        "HTTP request duration in seconds.",
        new[] { "service", "region", "method", "route" },
        new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(0.005, 2, 12) });

    public static readonly Counter CacheHits = Metrics.CreateCounter(
        "happy_headlines_cache_hits_total", "Cache hits by data layer and region.", new[] { "layer", "region" });

    public static readonly Counter CacheMisses = Metrics.CreateCounter(
        "happy_headlines_cache_misses_total", "Cache misses by data layer and region.", new[] { "layer", "region" });

    public static readonly Counter CacheFallbacks = Metrics.CreateCounter(
        "happy_headlines_cache_fallbacks_total", "Database fallbacks after a cache operation failed.", new[] { "layer", "region" });

    public static readonly Counter CacheBypasses = Metrics.CreateCounter(
        "happy_headlines_cache_bypasses_total", "Cache operations bypassed by an explicit feature toggle.", new[] { "layer", "region" });

    public static readonly Histogram CacheOperationDuration = Metrics.CreateHistogram(
        "happy_headlines_cache_operation_seconds", "Cache lookup and write duration in seconds.",
        new[] { "layer", "operation" },
        new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(0.0005, 2, 14) });

    public static readonly Counter ModerationChecks = Metrics.CreateCounter("happy_headlines_moderation_checks_total", "Moderation checks by result.", new[] { "result" });
    public static readonly Counter SubscriberEvents = Metrics.CreateCounter("happy_headlines_subscriber_events_total", "Subscriber notification events by outcome.", new[] { "outcome" });

    public static readonly Counter PublishedEvents = Metrics.CreateCounter(
        "happy_headlines_published_events_total", "Article publication events sent to RabbitMQ.");

    public static readonly Counter ConsumedEvents = Metrics.CreateCounter(
        "happy_headlines_consumed_events_total", "Article publication events consumed by services.");
}
