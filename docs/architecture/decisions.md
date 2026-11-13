# Architecture decisions

## Boundaries and communication

PublisherService, NewsletterService, ArticleService, CommentService, ProfanityService and SubscriberService have distinct responsibilities. RabbitMQ decouples article publication, persistence, and subscriber notification processing. SubscriberService has its own database and notification queue. A shared Redis-backed runtime flag controls subscriber endpoints and work. Disabled or unavailable state skips new work and pauses the queue; in-flight operations can finish. NewsletterService persists enabled subscriber work in its own database outbox and retries publication. Subscriber records are idempotent and skipped events are not backfilled. Notification processing records work rather than sending email. If the outbox database is unavailable while the feature is enabled, the current article is published to storage but its input event is retried; with a prefetch of one, later article events wait for the outbox database. When Redis is unavailable, the feature is treated as disabled and new subscriber work is skipped without backfill.

CommentService calls ProfanityService synchronously through a circuit breaker. After three failures the circuit opens for 15 seconds; one probe tests recovery after that interval. Each moderation call has a two-second timeout. Unavailable moderation fails closed rather than allowing an unchecked comment. These boundaries limit propagation but do not constitute fully independent physical deployments in the local Compose stack.

## Scale and caching

ArticleService replicas sit behind the NGINX gateway. A shared regional router selects separate EU and NA Article PostgreSQL connections for reads, writes, and prefill. The two databases demonstrate separate local failure and data partitions; the Compose stack is not a geographically distributed production deployment. Comment data remains in a separate PostgreSQL database. Redis uses region-qualified keys.

The prefill worker loads recent articles every six hours with an eight-hour cache TTL. It handles failures per region; after any failure the worker retries after 60 seconds by default (configurable from 1 to 300 seconds), then returns to the six-hour interval after a successful pass. Successful regions are also refreshed during retry passes. Compose waits for database containers to start, not for both databases to become healthy. Individual article reads and comment reads use cache-aside; comment writes invalidate the relevant list. The Redis recent-article index bounds comment lists per region. Cache failures fall back to PostgreSQL. These policies represent local design choices rather than capacity or energy measurements.

## Observability

Prometheus scrapes metrics and Grafana displays them. Seq stores structured logs and Zipkin displays distributed traces across the article publication path. Actual evidence requires observed telemetry from a running stack.
