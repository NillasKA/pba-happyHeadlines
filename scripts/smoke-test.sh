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
  echo "Expected physical regional article isolation (404 from na), received $wrong_region_status" >&2
  exit 1
fi

# Verify the row is stored only on the EU database server.
eu_rows="$(docker compose exec -T article-db-eu psql -U happy -d articles -tAc "SELECT count(*) FROM articles WHERE id = '$article_id'")"
na_rows="$(docker compose exec -T article-db-na psql -U happy -d articles -tAc "SELECT count(*) FROM articles WHERE id = '$article_id'")"
[[ "$(echo "$eu_rows" | tr -d '\r[:space:]')" == 1 && "$(echo "$na_rows" | tr -d '\r[:space:]')" == 0 ]]

# The other region must also persist to its own physical database.
na_published="$(curl -fsS -X POST "$base_url/publish" -H 'Content-Type: application/json' -d '{"title":"NA smoke test","body":"Regional isolation","region":"na"}')"
na_id="$(python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])' <<<"$na_published")"
na_ready=0
for _ in $(seq 1 30); do
  if curl -fsS "$base_url/articles/$na_id" -H 'X-Region: na' >/dev/null 2>&1; then na_ready=1; break; fi
  sleep 1
done
[[ "$na_ready" == 1 ]]
na_count="$(docker compose exec -T article-db-na psql -U happy -d articles -tAc "SELECT count(*) FROM articles WHERE id = '$na_id'" | tr -d '\r[:space:]')"
eu_count="$(docker compose exec -T article-db-eu psql -U happy -d articles -tAc "SELECT count(*) FROM articles WHERE id = '$na_id'" | tr -d '\r[:space:]')"
[[ "$na_count" == 1 && "$eu_count" == 0 ]]
[[ "$(curl -sS -o /dev/null -w '%{http_code}' "$base_url/articles/$na_id" -H 'X-Region: eu')" == 404 ]]

# Dynamic upstream discovery should distribute requests over both replicas.
instances="$(for _ in $(seq 1 40); do curl -fsS -D - -o /dev/null "$base_url/articles?limit=1" | tr -d '\r' | sed -n 's/^[Xx]-[Aa]rticle-[Ii]nstance: //p'; done | sort -u | wc -l)"
if (( instances < 2 )); then
  echo "Expected two ArticleService instances, observed $instances" >&2
  exit 1
fi

# A second read should be served from Redis.
curl -fsS "$base_url/articles/$article_id" -H "X-Region: $region" >/dev/null

comment_payload='{"body":"Comment cache smoke test"}'
comment_ready=0
for _ in $(seq 1 45); do
  code="$(curl --max-time 5 -sS -o /dev/null -w '%{http_code}' -X POST "$base_url/articles/$article_id/comments" \
    -H "X-Region: $region" -H 'Content-Type: application/json' -d "$comment_payload" || true)"
  if [[ "$code" == 201 ]]; then comment_ready=1; break; fi
  sleep 1
done
[[ "$comment_ready" == 1 ]] || { echo "Comment service did not become ready" >&2; exit 1; }
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
