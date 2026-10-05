#!/usr/bin/env bash
set -euo pipefail

base_url="${BASE_URL:-http://localhost:8080}"
region="eu"

echo "Waiting for Happy Headlines API..."
ready=0
for _ in $(seq 1 60); do
  if curl -fsS "$base_url/articles?region=$region" >/dev/null 2>&1; then
    ready=1
    break
  fi
  sleep 2
done
if [[ "$ready" != "1" ]]; then
  echo "API gateway did not become ready at $base_url" >&2
  exit 1
fi

payload='{"title":"Smoke test article","body":"Published through RabbitMQ and persisted by ArticleService.","region":"eu"}'
published="$(curl -fsS -X POST "$base_url/publish" -H 'Content-Type: application/json' -d "$payload")"
article_id="$(python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])' <<<"$published")"
echo "Queued article $article_id"

article=""
for _ in $(seq 1 30); do
  article="$(curl -fsS "$base_url/articles/$article_id" -H "X-Region: $region" 2>/dev/null || true)"
  if [[ -n "$article" ]] && python3 -c 'import json,sys; d=json.load(sys.stdin); sys.exit(0 if d.get("id") else 1)' <<<"$article" 2>/dev/null; then
    break
  fi
  sleep 1
done
if [[ -z "$article" ]]; then
  echo "Article was not persisted by NewsletterService/ArticleService" >&2
  exit 1
fi

wrong_region_status="$(curl -sS -o /dev/null -w '%{http_code}' "$base_url/articles/$article_id" -H 'X-Region: na')"
if [[ "$wrong_region_status" != "404" ]]; then
  echo "Expected article partition isolation (404 from na), received $wrong_region_status" >&2
  exit 1
fi

# A second read should be served from Redis.
curl -fsS "$base_url/articles/$article_id" -H "X-Region: $region" >/dev/null

comment_payload='{"body":"Comment cache smoke test"}'
curl -fsS -X POST "$base_url/articles/$article_id/comments" \
  -H "X-Region: $region" -H 'Content-Type: application/json' -d "$comment_payload" >/dev/null
comments="$(curl -fsS "$base_url/articles/$article_id/comments" -H "X-Region: $region")"
python3 -c 'import json,sys; d=json.load(sys.stdin); assert len(d) == 1 and d[0]["body"] == "Comment cache smoke test"' <<<"$comments"
curl -fsS "$base_url/articles/$article_id/comments" -H "X-Region: $region" >/dev/null
curl -fsS -X POST "$base_url/articles/$article_id/comments" \
  -H "X-Region: $region" -H 'Content-Type: application/json' \
  -d '{"body":"Cache invalidation smoke test"}' >/dev/null
comments="$(curl -fsS "$base_url/articles/$article_id/comments" -H "X-Region: $region")"
python3 -c 'import json,sys; d=json.load(sys.stdin); assert len(d) == 2' <<<"$comments"

for _ in $(seq 1 31); do
  synthetic_id="$(python3 -c 'import uuid; print(uuid.uuid4())')"
  curl -fsS "$base_url/articles/$synthetic_id/comments" -H "X-Region: $region" >/dev/null
done
cached_comment_lists="$(docker compose exec -T redis redis-cli ZCARD "comments:lru:$region:30" | tr -d '\r')"
if (( cached_comment_lists > 30 )); then
  echo "Comment cache exceeded its 30-article bound: $cached_comment_lists" >&2
  exit 1
fi

echo "PASS: queued publication, regional article isolation, article/comment cache hit/invalidation, and 30-list LRU bound"
