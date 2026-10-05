# Architecture decisions

## Service boundaries

- **PublisherService** accepts article publication requests and emits an `ArticlePublished` event.
- **RabbitMQ / ArticleQueue** decouples publication from processing.
- **NewsletterService** consumes the event and republishes an article-storage command to a second durable queue.
- **ArticleService** owns article persistence and article caching.
- **Article Prefill Job** is a single scheduled worker that preloads recent articles, avoiding one prefill loop per X-axis ArticleService replica.
- **CommentService** owns comment persistence and comment caching.
- ArticleService and CommentService use separate PostgreSQL containers to avoid sharing a persistence failure domain.

The publication and persistence path is asynchronous across both service boundaries. Trace context is propagated through RabbitMQ message headers. Synchronous HTTP is used at the API gateway edge for user request/response paths.

## Scale Cube demonstration

- **X-axis:** stateless service instances can be scaled horizontally behind the gateway. Database reads are the first candidate for replication; writes remain authoritative.
- **Y-axis:** article publication, newsletter processing, article reads, and comment reads are distinct responsibilities.
- **Z-axis:** requests are routed by a validated `eu` or `na` region key. PostgreSQL tables and Redis keys are partitioned by region. The local deployment demonstrates logical partitioning; independent production regions would need additional deployment, replication, and failure-recovery design.

The Z-axis choice is region because it connects the Week 36 partitioning exercise to Week 40's geo-latency scenario. Routing must be centralized; clients do not select database hosts directly.

## Caching decisions

- Articles: scheduled prefill of the last 14 days every 6 hours; 8-hour TTL.
- Comments: cache-aside on a miss; 15-minute initial TTL; invalidate on comment writes.
- Comment recency: maintain a bounded per-region set of the 30 most recently used article IDs; Redis memory policy is an additional safeguard, not the exact-count mechanism.
- Redis failure: fall back to PostgreSQL and count the fallback. Cache errors must not make article/comment reads unavailable.
- Cache keys include data type and region to avoid collisions.

## Observability

- Prometheus answers trend/threshold questions from service and cache metrics.
- Grafana visualizes hits, misses, hit ratio, latency, request volume, fallbacks, and errors.
- Seq stores structured logs with service, request, and trace identifiers.
- Zipkin displays distributed traces across PublisherService, RabbitMQ, NewsletterService, and ArticleService.

This uses the course monitoring sequence: **Is there an incident? → Where is the problem? → What is the problem?**
