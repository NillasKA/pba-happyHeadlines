# Week 40 Happy Headlines presentation guide

This is a speaker guide for the Week 40 compulsory assignment. It assumes the audience wants to understand the **large-system choices** and how they were verified, not every line of application code.

The notes say this is a live Zoom presentation with no compulsory file hand-in; they list Tuesday, October 6, with the exact slot on Moodle. Confirm your allotted slot and any updated instructions there. The outline below is modular: use the core sections for a short slot and the optional reliability demo if you have more time.

## The message to leave with the instructor

> We use two cache-population strategies that fit different access patterns: scheduled prefill for recent articles and read-on-miss for comments. Redis is shared by service instances, cache freshness is bounded, and we use measured hit ratios and latency to show whether caching helps. PostgreSQL remains authoritative, so reads still work when Redis is unavailable.

## Quick vocabulary

- **Hit:** Redis had the requested value. **Miss:** the value was absent, so the service read PostgreSQL.
- **TTL:** how long a cached value may live before expiring.
- **Prefill:** scheduled loading of data into the cache before it is requested.
- **Read-on-miss/cache-aside:** load the value from PostgreSQL after a miss, then put it in Redis.
- **LRU:** least recently used; discard the least recently accessed cached article list when enforcing the 30-list limit.
- **Hit ratio:** hits divided by hits plus misses over the same interval.

The course’s grocery-store analogy works well: Redis is the nearby shelf; PostgreSQL is the larger stockroom. A hit avoids the longer trip to the source.

## Suggested talk track

### 1. State the problem

**Say:** “The Week 40 scenario is that reads from Europe are slow because the source database is far away in North America. The target design is to serve common reads from a nearby regional cache, reduce database round trips, and measure whether the cache is useful.”

**Show:** the C4 container view or the README architecture diagram. Point out the gateway, ArticleService and CommentService, their PostgreSQL stores, shared Redis, and Prometheus/Grafana.

**Explain:** Redis is a read-performance layer, not the authority for article or comment data. ArticleService and CommentService own separate PostgreSQL containers. In the local demo, one Redis instance uses region-qualified keys and PostgreSQL uses logical `eu`/`na` partitions. This demonstrates the routing and namespacing choices, but it does **not** physically place caches or databases near European and North American users.

### 2. Explain the two caching patterns

#### Articles: scheduled prefill

**Say:** “Articles are predictable and recent articles are likely to be read, so a single Article Prefill Job loads the last 14 days every six hours. Each entry has an eight-hour TTL. That gives the next run some overlap to complete before the previous entries expire.”

Point out that the worker is a separate process. If ArticleService is scaled to multiple replicas, it does not start a prefill loop in every replica.

#### Comments: read-on-miss

**Say:** “Comment traffic is more selective, so we only cache a list after someone requests it. The first request fetches from PostgreSQL; following requests can use Redis. Comment writes invalidate that article’s cached list.”

The comment cache uses a 15-minute TTL and maintains at most 30 recently used article IDs per region. The Redis `allkeys-lru` policy bounds memory as a second safeguard; the application’s recency index enforces the exact 30-list limit.

### 3. Explain freshness, eviction, and failure handling

Cover these tradeoffs explicitly:

- **Freshness:** article TTL is eight hours with a six-hour refresh; comment TTL is shorter and writes invalidate the relevant list.
- **Eviction:** the comment cache tracks the 30 most recently accessed article lists; Redis also has a memory cap and LRU eviction policy.
- **Redis failure:** PostgreSQL remains the source of truth. The service records a fallback metric and reads from its database when Redis is unavailable.
- **Stampede:** concurrent misses within a service instance are coalesced. Across many replicas, a distributed lock or randomized refresh could reduce duplicate work further; the local demo keeps this mitigation bounded and understandable.
- **Key collisions:** cache keys include both data type and region, for example `article:eu:<id>` and `comments:eu:<article-id>`.

### 4. Explain how the dashboard proves the design

Open Grafana at <http://localhost:3000>, then select **Happy Headlines — Cache and Service Health**.

**Say:** “A hit ratio only means something when we show both hits and misses. We calculate the ratio over rates of counter changes, so restarts and older cumulative values don’t make the gauge misleading.”

The PromQL idea is:

```promql
rate(happy_headlines_cache_hits_total[5m])
  / (rate(happy_headlines_cache_hits_total[5m]) + rate(happy_headlines_cache_misses_total[5m]))
```

The provisioned dashboard also separates layers and regions and includes request volume, p95 HTTP/cache latency, server errors, cache fallbacks, and cache bypasses. Prometheus is the metrics source; Zipkin is used separately to inspect traces, and Seq is used to inspect structured logs.

### 5. Demonstrate it

Before the presentation, build and start the stack:

```bash
./scripts/build.sh
docker compose up --build -d --wait
```

Check services with `docker compose ps`. For the live demonstration, publish an article:

```bash
curl -sS -X POST http://localhost:8080/publish \
  -H 'Content-Type: application/json' \
  -d '{"title":"Demo headline","body":"A short demonstration article.","region":"eu"}'
```

The response is `202 Accepted` with an article ID because publication continues asynchronously. Copy the ID into a variable, then wait for the queue consumers to persist it:

```bash
article_id='paste-the-returned-id-here'
for _ in $(seq 1 30); do
  if curl -fsS "http://localhost:8080/articles/$article_id" -H 'X-Region: eu' >/dev/null; then break; fi
  sleep 1
done
```

The first article read after publication should miss the cache because the write invalidates the article key; the next should hit. If you want to guarantee a cold read, delete that key first:

```bash
docker compose exec redis redis-cli DEL "article:eu:$article_id"
curl -i "http://localhost:8080/articles/$article_id" -H 'X-Region: eu'
curl -i "http://localhost:8080/articles/$article_id" -H 'X-Region: eu'
```

Now add a comment and read the list twice:

```bash
curl -i -X POST "http://localhost:8080/articles/$article_id/comments" \
  -H 'X-Region: eu' -H 'Content-Type: application/json' \
  -d '{"body":"First demo comment."}'
curl -i "http://localhost:8080/articles/$article_id/comments" -H 'X-Region: eu'
curl -i "http://localhost:8080/articles/$article_id/comments" -H 'X-Region: eu'
```

Show the relevant Grafana panels. Then open Zipkin at <http://localhost:9411> and find the trace from PublisherService through NewsletterService to ArticleService. If time permits, inspect the cache TTL or the 30-item comment recency index in Redis.

#### Optional: demonstrate Redis failure fallback

This is useful for discussing failure behavior, but it temporarily stops Redis. Restore it immediately after the read:

```bash
docker compose stop redis
curl -i "http://localhost:8080/articles/$article_id" -H 'X-Region: eu'
docker compose start redis
```

The read should still succeed from PostgreSQL, though it may take longer. Grafana should show a cache fallback. Don’t stop Redis if the stack is already being used for another demonstration.

### 6. Close with the design rationale

**Say:** “We did not cache every read indiscriminately. We chose prefill for the predictable recent-article window and read-on-miss for comments. TTL and bounded eviction control staleness and memory, while metrics let us judge effectiveness and database fallback protects availability.”

Then connect it to the course: the shared Redis avoids per-instance cache inconsistency; the separate services express Y-axis boundaries; and regional data/key routing demonstrates the Z-axis. The smoke test checks those behaviors, while k6 provides a repeatable baseline rather than a production-capacity claim.

## Likely questions and concise answers

**Why prefill articles but not comments?** Recent articles are a predictable set that benefits from being ready before readers request them. Comments are fetched only for article IDs that users actually visit.

**Why is the article TTL longer than the prefill interval?** It leaves overlap for the prefill job to complete before older entries expire; it is still a bound on staleness.

**What makes the comment limit exactly 30?** A Redis sorted-set recency index and atomic pruning script. `maxmemory`/`allkeys-lru` alone would limit bytes, not guarantee exactly 30 article IDs.

**What if Redis is unavailable?** The data services fall back to their owning PostgreSQL database and increment a fallback counter. Redis is an optimization, not the source of truth.

**How do you know caching helps?** Compare hits and misses by layer and region, and look at request/latency behavior. A high hit ratio by itself doesn’t prove that user-visible latency improved.

**Is this a real multi-region deployment?** No. The local setup demonstrates region-keyed routing and partitions on one Compose network. Real regions would need separate deployments, replication, and failover planning.

## Week 40 evaluation checklist

- Explain and demonstrate scheduled article prefill versus comment read-on-miss.
- Justify article/comment TTL and the bounded eviction policy.
- Show separate, correctly calculated article/comment hit ratios and request volume by region.
- Explain stale data, cache stampede mitigation, Redis failure, and database fallback.
- Show the relevant Grafana panels and, if asked, the trace/log evidence.
- Confirm the Zoom slot and any changed assessment instructions in Moodle.
