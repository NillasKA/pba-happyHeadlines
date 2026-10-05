# Project workflow and course mapping

## End-to-end article path

1. A publisher submits an article to PublisherService.
2. PublisherService validates the request and publishes an `ArticlePublished` message to RabbitMQ.
3. NewsletterService consumes the message, records the newsletter action, and republishes it to the durable `article.to-store` queue.
4. ArticleService consumes that command, stores the article in its regional PostgreSQL partition, and invalidates the article cache.
5. Readers obtain articles through ArticleService; comments are obtained through CommentService.

The Article Prefill Job runs separately from ArticleService replicas. It reads recent rows from the article database and fills the shared Redis cache on the six-hour schedule, so horizontal scaling does not multiply scheduled prefill work.

## Course sequence

- Week 35: Docker multi-stage builds, Compose orchestration/health, C4 diagrams.
- Week 36: headroom analysis, X-axis service replicas, Z-axis region partitioning.
- Week 37: asynchronous design, failure isolation, rollback/disable/monitor principles, load test.
- Week 38: design-to-be-monitored, structured logging, metrics, and C4 monitoring components.
- Week 39: RabbitMQ message path and end-to-end trace propagation across the publish and persistence queues.
- Week 40: Redis prefill/read-on-miss, TTL, bounded eviction, Prometheus metrics, Grafana dashboard.
- Week 41: organisational evaluation concepts inform service ownership and project workflow; the weekly exercise itself is not a runtime feature.
