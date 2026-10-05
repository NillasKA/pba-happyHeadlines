# Week 37 scalability principles in Happy Headlines

| Principle | Project implementation / evidence |
|---|---|
| Asynchronous design | PublisherService sends a durable event to RabbitMQ; NewsletterService republishes an article-storage command; ArticleService consumes it. Neither service blocks the publisher on article persistence. |
| Automation over people | `scripts/build.sh`, Docker Compose, automated smoke test, and k6 load script. |
| Scale out, not up | API services are stateless and can be cloned with Compose `--scale`; the gateway resolves service replicas dynamically. |
| Design for rollback | Rebuild/redeploy a prior project revision to roll back. Cache flags allow bypassing either cache without changing the article/comment API. |
| Design to be disabled | Set `Caching__ArticleEnabled=false` or `Caching__CommentEnabled=false`; reads go directly to the owning PostgreSQL database. |
| Design to be monitored | Prometheus counters/histograms, structured Seq logs, OpenTelemetry spans, Zipkin, and a provisioned Grafana dashboard. |
| Design for at least two axes | Demonstrate X-axis replicas and a Z-axis region partition; Y-axis boundaries are independently owned services. |
| Build small, release fast, fail small | Independent service containers, queue-based decoupling, and a k6 threshold baseline. |
| Isolate faults with swimlanes | Article and comment data use separate PostgreSQL services; queues separate publication, newsletter handling, and persistence. Redis failures fall back to the relevant database. |

The Compose deployment runs on one machine, so it demonstrates service/data boundaries and replica behavior—not physical failure isolation across regions or hosts.
