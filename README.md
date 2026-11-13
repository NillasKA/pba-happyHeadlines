# Happy Headlines

Happy Headlines is a distributed article publishing project for Development of Large Systems. It demonstrates service boundaries, asynchronous messaging, regional database sharding, horizontal scaling, fault isolation, observability, caching, feature toggles, and delivery automation.

## Architecture

PublisherService places article events on RabbitMQ. NewsletterService forwards them to ArticleService for persistence and, when enabled, stores subscriber work in a durable outbox for publication to a separate queue. SubscriberService stores subscriber data and records notification processing; it does not send email. CommentService checks comment text through ProfanityService, using a circuit breaker when moderation is unavailable. ArticleService uses separate EU and NA PostgreSQL databases selected by a shared region router. The prefill worker warms Redis separately from ArticleService replicas.

[Architecture views](docs/architecture/workspace.dsl) contain a C4 container diagram and an ArticleService component diagram. The [assignment requirements](docs/assignment/requirements.md), [hand-in guide](docs/assignment/hand-in.md), and [sustainability evidence](docs/assignment/sustainability.md) map the implementation to the compulsory submission and identify the evidence still needed.

## Project layout

- `src/PublisherService/`, `src/NewsletterService/`, `src/ArticleService/`, `src/CommentService/`, `src/ProfanityService/`, and `src/SubscriberService/` contain the six application services.
- `src/PrefillJob/` contains the scheduled worker; `src/Shared/` contains shared contracts and infrastructure.
- `infra/` contains the gateway, databases, and monitoring configuration.
- `docs/architecture/` contains the C4 model and architectural decisions; `docs/assignment/` contains submission guidance.
- `tests/` and `scripts/` contain automated checks and build helpers.

## Build and start

Prerequisites are the .NET 10 SDK, Docker Engine, Docker Compose v2, Bash, curl and Python 3. From the repository root:

```bash
bash scripts/build.sh
docker compose up --build -d --wait --scale article-service=2
docker compose ps
```

The build script publishes all six services and the prefill worker to `artifacts/`; the Compose images use those outputs. Rebuild the artifacts after application changes. The two regional article volumes are new; data in an older single article database volume is not migrated automatically. To build and run the available tests:

```bash
dotnet build HappyHeadlines.slnx
dotnet test HappyHeadlines.slnx
bash scripts/smoke-test.sh
```

The smoke test requires the running stack with at least two ArticleService replicas. A local scale-out demonstration uses `docker compose up --build -d --wait --scale article-service=2`. The optional load test uses `docker compose --profile loadtest run --rm --no-deps k6`. Local results do not establish production capacity.

## Local endpoints

| Endpoint | Purpose |
|---|---|
| <http://localhost:8080> | API gateway |
| <http://localhost:8081> | Structurizr Lite |
| <http://localhost:3000> | Grafana (`admin` / `happy-headlines`) |
| <http://localhost:9090> | Prometheus |
| <http://localhost:8083> | Seq logs |
| <http://localhost:9411> | Zipkin traces |
| <http://localhost:15672> | RabbitMQ management (`guest` / `guest`) |

`POST /publish` accepts a JSON article and returns its ID before asynchronous persistence. `GET /articles?region=eu` and `GET /articles/{id}` read articles; `X-Region: eu` or `X-Region: na` selects a region. Comment routes use `/articles/{id}/comments`. The gateway also exposes `/subscribers` and `/features/subscribers`.

The subscriber feature defaults to disabled. `GET /features/subscribers` reads its state; `PUT /features/subscribers` accepts `{"enabled":true}` or `{"enabled":false}`. The state is shared in Redis with AOF persistence; absent or unavailable state disables subscriber interactions. When disabled, subscriber endpoints return 503, new subscriber work is skipped, and queued work is paused. Operations already in flight can finish, and skipped events are not backfilled. Outbox publication retries may duplicate delivery; subscriber records are deduplicated.

## Delivery

`.github/workflows/delivery.yml` defines build, test and smoke checks and publishes seven versioned container images on a successful `v*` tag. A separate manually triggered promotion workflow deploys a selected image tag to a configured host. Publication and deployment require separate verification and host credentials; no deployment is implied by the configuration.

## Monitoring and shutdown

Grafana displays service and cache metrics; Seq stores structured logs; Zipkin displays traces across the publication path. The [hand-in guide](docs/assignment/hand-in.md) describes the screenshots required by the assignment.

`docker compose down` stops the stack and retains volumes. `docker compose down -v` also removes local persisted data, including database and monitoring volumes.
