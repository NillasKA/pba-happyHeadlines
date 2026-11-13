# Hand-in guide

The fields follow the [official assignment](https://moodle.easv.dk/mod/assign/view.php?id=397622). The displayed deadline is Monday, 16 November 2026, 8:00 AM. The course evaluation precedes access to the assignment. Evidence is to represent the actual running project and repository revision.

## Repository

The field requires the public repository URL. Public visibility requires confirmation without account access.

## C4 diagrams

The `containers` view in Structurizr at <http://localhost:8081> supplies the level 2 diagram; `article-components` supplies ArticleService level 3. Both views require legible screenshots and verification against the running revision.

## Scale cube

- **Z-axis coordinator:** `src/Shared/Configuration/ArticleShardRouter.cs` and the connection strings in `compose.yaml` provide the coordinator and its configuration. The router normalizes the region and selects the EU or NA connection pool. `ArticleRepository` uses it for reads and writes; the separate prefill repository uses it for both regions.
- **X-axis:** `compose.yaml` and `infra/nginx/default.conf` contain the ArticleService and gateway configuration. The scaling command is `docker compose up -d --scale article-service=2`. NGINX resolves the Compose service name dynamically and uses its default round-robin selection across resolved instances. The `X-Article-Instance` response header identifies the serving instance. Evidence of routing to both replicas requires runtime observation.

## Fault isolation

`src/CommentService/Services/ProfanityClient.cs` contains the circuit breaker; registration is in `src/CommentService/Program.cs`. The breaker opens after three failed checks, waits 15 seconds, and allows one recovery probe. Calls time out after two seconds. Unavailable moderation produces a 503 rather than accepting an unchecked comment; rejected content produces a 400. This separates the moderation dependency from other paths and bounds failed moderation calls. The breaker is in-process per CommentService instance. Observed test results require separate verification.

## Monitoring

After an article publication and read, the monitoring gallery requires:

1. A Grafana or Prometheus screenshot identifying the service, metric and time interval.
2. A Seq screenshot identifying the service and relevant structured log event.
3. A Zipkin screenshot identifying participating services and messaging spans in a distributed trace.

Each image requires a description. Empty dashboards or configured endpoints alone do not demonstrate observed telemetry. The verification workflow attempts to capture live screenshots under `artifacts/evidence/` and uploads available evidence. The captured images require review for legibility and relevance; if capture fails, the monitoring tools and Structurizr provide the manual fallback.

## DevOps

The `verify` job in `.github/workflows/delivery.yml` supplies the CI field; `publish` supplies the CD field. CI restores, builds, tests, builds the Compose stack and runs the smoke test. On a pushed `v*` tag, a successful verify job permits seven tagged images to be published to GHCR. A separate manually triggered `.github/workflows/promote.yml` deploys a selected image tag to a configured host. A successful build is distinct from successful registry publication or remote deployment.

## Feature toggle

`src/NewsletterService/Services/NewsletterService.cs` contains the toggle decision; `src/Shared/Services/SubscriberFeature.cs` contains shared Redis state. When enabled, subscriber work is saved in the NewsletterService outbox and dispatched to the separate queue. When disabled or unavailable, subscriber endpoints return 503, new subscriber work is skipped and queued processing pauses. The state is persistent in the local Redis volume, although in-flight operations can finish and skipped events are not backfilled. Both paths require runtime verification.

## Sustainability

The [sustainability evidence](sustainability.md) describes three tactics and their before/after code. The gallery requires at least three pictures; each description names the tactic, shows the actual improvement and explains its effect. Numerical savings require measurements.

## Recording and final submission

A possible five-minute tour allocates about 45 seconds to architecture, 75 seconds to scaling and fault isolation, 60 seconds to observability, 60 seconds to delivery and the feature toggle, and the remaining minute to selected sustainability changes. A shorter selection is appropriate if necessary. The recording requires course vocabulary and actual evidence; the official limit is five minutes.

Completion requires the assignment fields, screenshots and recording, followed by **Add submission** and the short message described in the lecture. The resulting submission status requires confirmation in Moodle.
