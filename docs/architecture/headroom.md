# Headroom and load-test notes

Use the Week 36 / ART Chapter 11 method against measured project traffic rather than assuming the classroom sample numbers describe this application.

For each service pool and data tier, record:

| Component | Current peak | Tested maximum | Planning threshold | Growth assumption | Planned reclamation |
|---|---:|---:|---:|---:|---:|
| Article API | Measure with k6 | Measure with k6 | 50% initially | Set after baseline | Redis/cache effect |
| Comment API | Measure with k6 | Measure with k6 | 50% initially | Set after baseline | Redis/cache effect |
| Article PostgreSQL | Measure queries/s | Measure with load test | 50% initially | Set after baseline | Article cache |
| Comment PostgreSQL | Measure queries/s | Measure with load test | 50% initially | Set after baseline | Comment cache |

Formula per component:

```text
headroom = (ideal usage × tested maximum) − current peak − forecast growth + planned reclaimed capacity
```

Initial local baseline (10 virtual users for 30 seconds, one article-service instance, article and comment read paths): **96.2 HTTP requests/second**, **p95 6.73 ms**, and **0% HTTP failures**. This is a baseline for comparison, not the tested maximum capacity. Re-run after changes and raise load until the chosen response-time/error objectives stop being met before estimating headroom. Cache hit ratio by itself does not establish useful headroom if latency or correctness worsens.

## X-axis demonstration

Compose can start duplicate instances without fixed host-port mappings:

```bash
./scripts/build.sh
docker compose up --build -d --wait --scale article-service=2
```

The NGINX gateway resolves service names through Docker DNS. Check `docker compose ps` for both ArticleService instances and repeat the smoke test through the same gateway.
