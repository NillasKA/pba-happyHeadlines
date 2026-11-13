# Compulsory assignment 2: hand-in pack

The [official assignment](https://moodle.easv.dk/mod/assign/view.php?id=397622) displays the deadline **Monday, 16 November 2026, 8:00 AM**. The course evaluation precedes assignment access. The material below identifies the code and proposed explanations for each field. The evidence was captured from a successful [verification run](https://github.com/NillasKA/pba-happyHeadlines/actions/runs/43422690611) for revision `7a0fc14b0bfaafaffa815c7f9a3125bae595c744`. The remaining steps are the Moodle fields and image uploads, the recording, and the final submission.

## GitHub repository

Repository: <https://github.com/NillasKA/pba-happyHeadlines>

The repository is public. The verified source revision is recorded in the [capture manifest](screenshots/manifest.json).

## C4 diagrams

The two required screenshots are [c4-containers.png](screenshots/c4-containers.png) (level 2) and [c4-article-components.png](screenshots/c4-article-components.png) (ArticleService level 3). The [Structurizr model](../architecture/workspace.dsl) provides the `containers` and `article-components` views at <http://localhost:8081/workspace/diagrams>. The captured diagrams were visually reviewed.

## Scale cube: Z-axis coordination code

The coordinator code is [`ArticleShardRouter.cs`](../../src/Shared/Configuration/ArticleShardRouter.cs). The relevant EU/NA connection configuration is in [`compose.yaml`](../../compose.yaml), and the repository uses the router for both reads and writes in [`ArticleRepository.cs`](../../src/ArticleService/Infrastructure/ArticleRepository.cs).

Field explanation:

> ArticleShardRouter normalizes the region and selects the EU or NA PostgreSQL connection pool. ArticleService uses the same selection for reads and writes; the independent prefill worker reads both regional databases. The databases are separate containers in the local deployment, rather than geographically distributed regions.

## Scale cube: X-axis code and load-balancer explanation

The [`article-service` and `gateway` definitions](../../compose.yaml) and [`infra/nginx/default.conf`](../../infra/nginx/default.conf) provide the configuration. Scaling is expressed as `docker compose up -d --scale article-service=2`.

Field explanation:

> NGINX accepts requests at the gateway and forwards article requests to the ArticleService upstream. Docker DNS supplies the addresses for the service replicas, and NGINX resolves those addresses dynamically. The upstream uses the default round-robin selection across the resolved instances. Each ArticleService response includes an `X-Article-Instance` header that can identify the selected replica. The [integration smoke test](../../scripts/smoke-test.sh) checks the serving-instance headers for two distinct instances; its [run log](screenshots/smoke.log) reports a pass.

## Fault isolation: circuit breaker and swimlanes

The complete circuit breaker is [`ProfanityClient.cs`](../../src/CommentService/Services/ProfanityClient.cs); registration is in [`CommentService/Program.cs`](../../src/CommentService/Program.cs). The controller maps moderation unavailability to HTTP 503 and rejected text to 400.

Field explanation:

> CommentService calls the separate ProfanityService through a circuit breaker. After three failures, the breaker opens for 15 seconds and rejects further moderation calls; one half-open probe then tests recovery. A two-second timeout bounds each attempted call. If moderation is unavailable, a comment write fails closed with 503 rather than saving unchecked text. The moderation dependency is isolated from unrelated article operations, illustrating separate swimlanes and limiting failure propagation. The breaker state is local to each CommentService instance.

## Monitoring gallery

The following screenshots were captured from the verified run and visually reviewed.

| File | Intended description |
|---|---|
| [metrics-prometheus.png](screenshots/metrics-prometheus.png) | Prometheus query showing Happy Headlines HTTP request counts by service. The visible service labels and values identify the measured activity. |
| [logging-seq.png](screenshots/logging-seq.png) | Seq structured events from article publication or persistence, identifying the emitting service and the operation recorded. |
| [tracing-zipkin.png](screenshots/tracing-zipkin.png) | Zipkin distributed trace showing spans from multiple services in an article publication path. The visible span names and services identify the components involved. |

The [verification run](https://github.com/NillasKA/pba-happyHeadlines/actions/runs/43422690611) also retains its `verification-evidence` artifact.

## DevOps: CI and CD code fields

The CI field uses the complete `verify` job and its workflow context in [`.github/workflows/delivery.yml`](../../.github/workflows/delivery.yml). It restores, builds, tests, starts two ArticleService replicas, runs the integration smoke test and attempts evidence capture.

The CD field uses the `publish` job and its workflow context from the same file, together with [`.github/workflows/promote.yml`](../../.github/workflows/promote.yml). A successful verification permits `v*` tags to publish seven versioned images to GHCR. The separate manually triggered promotion deploys a selected release to a configured host. Publication and deployment require separate confirmation; workflow definitions do not establish that either occurred.

## NewsletterService feature-toggle code

The required NewsletterService decision is in [`NewsletterService.cs`](../../src/NewsletterService/Services/NewsletterService.cs):

```csharp
await articleStorePublisher.PublishAsync(article, cancellationToken);
if (await feature.IsEnabledAsync(cancellationToken))
    await outbox.EnqueueAsync(article, cancellationToken);
else HappyMetrics.SubscriberEvents.WithLabels("disabled").Inc();
```

[`SubscriberFeature.cs`](../../src/Shared/Services/SubscriberFeature.cs) stores the shared flag in Redis. Disabled or unavailable state blocks subscriber API operations with 503, skips new subscriber work and pauses pending queue processing. Work already in flight can finish; skipped events are not backfilled. The outbox retries publication, and SubscriberService deduplicates notification records. The enabled and disabled paths were exercised in the [resilience smoke test](screenshots/resilience.log).

## Sustainability gallery

The [sustainability evidence](sustainability.md) contains baseline/current code and the three tactics. The screenshots and descriptions are:

| File | Tactic and explanation |
|---|---|
| [gaf-pagination.png](screenshots/gaf-pagination.png) | **Defer Fetching/Lazy Fetch:** the article list supports a limit and offset, allowing a client to request a smaller page instead of the complete recent list. The measured full response contained six articles and 5,600 bytes; one article occupied 1,681 bytes. |
| [gaf-compression.png](screenshots/gaf-compression.png) | **Reduce Network Package Size:** ArticleService negotiates response compression. The measured gzip response was 627 bytes compared with 5,600 bytes without compression. Compression uses CPU and does not itself establish an energy saving. |
| [gaf-image.png](screenshots/gaf-image.png) | **Reduce Bundle Size:** service images use a chiseled runtime base rather than the previous full runtime base. On the Linux x64 runner the full and chiseled base images measured 97,675,516 and 72,585,216 bytes respectively. Image size alone does not establish runtime energy savings. |

The [measurement log](screenshots/gaf-measurements.log) records the values; the [manifest](screenshots/manifest.json) records the tested revision and platform. These are local observations, not measured energy or carbon savings.

## Recording and submission

A five-minute outline can allocate 45 seconds to the C4 views, 75 seconds to scale and fault isolation, 60 seconds to monitoring, 60 seconds to CI/CD and the feature toggle, and the remaining minute to selected sustainability evidence. The explanation should use course terminology and the actual submitted artifacts; fewer areas may be selected if necessary. The official maximum is five minutes.

The remaining personal steps are completion of the assignment fields, review and upload of the screenshots, recording and upload of the video, **Add submission** with the short message described in the lecture, and confirmation of the resulting Moodle submission status.
