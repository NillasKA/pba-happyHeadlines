#!/usr/bin/env bash
set -euo pipefail
base_url="${BASE_URL:-http://localhost:8080}"
subscriber_stopped=0
profanity_stopped=0
original_feature="$(curl --max-time 5 -fsS "$base_url/features/subscribers" | python3 -c 'import json,sys; print(str(json.load(sys.stdin)["enabled"]).lower())')"
cleanup() {
  set +e
  if (( subscriber_stopped )); then docker compose start subscriber-service >/dev/null; fi
  if (( profanity_stopped )); then docker compose start profanity-service >/dev/null; fi
  curl --max-time 5 -fsS -X PUT "$base_url/features/subscribers" -H 'Content-Type: application/json' -d "{\"enabled\":$original_feature}" >/dev/null
}
trap cleanup EXIT
set_feature() {
  curl --max-time 5 -fsS -X PUT "$base_url/features/subscribers" -H 'Content-Type: application/json' -d "{\"enabled\":$1}" >/dev/null
}
status() { curl --max-time 5 -sS -o /dev/null -w '%{http_code}' "$@"; }
assert_status() {
  local expected="$1" actual; shift
  actual="$(status "$@")"
  if [[ "$actual" != "$expected" ]]; then echo "Expected HTTP $expected, got $actual" >&2; return 1; fi
}
publish() {
  local response
  response="$(curl --max-time 5 -fsS -X POST "$base_url/publish" -H 'Content-Type: application/json' -d '{"title":"Resilience smoke test","body":"Synthetic test publication","region":"eu"}')"
  python3 -c 'import json,sys,uuid; print(uuid.UUID(json.load(sys.stdin)["id"]))' <<<"$response"
}
wait_article() {
  local id="$1"
  for _ in $(seq 1 60); do
    if curl --max-time 5 -fsS "$base_url/articles/$id" -H 'X-Region: eu' >/dev/null 2>&1; then return 0; fi
    sleep 1
  done
  echo "Article $id was not persisted" >&2; return 1
}
notifications() {
  docker compose exec -T subscriber-db psql -U happy -d subscribers -tAc "SELECT count(*) FROM subscriber_notifications WHERE article_id = '$1' AND subscriber_id = '$subscriber_id'" | tr -d '\r[:space:]'
}
wait_notification() {
  for _ in $(seq 1 60); do
    if [[ "$(notifications "$1" 2>/dev/null || true)" == 1 ]]; then return 0; fi
    sleep 1
  done
  echo "Notification for article $1 did not arrive" >&2; return 1
}
wait_outbox_sent() {
  local count
  for _ in $(seq 1 60); do
    count="$(docker compose exec -T newsletter-db psql -U happy -d newsletter -tAc "SELECT count(*) FROM subscriber_outbox WHERE article_id = '$1' AND sent_at IS NOT NULL" 2>/dev/null | tr -d '\r[:space:]' || true)"
    if [[ "$count" == 1 ]]; then return 0; fi
    sleep 1
  done
  echo "Outbox did not dispatch article $1" >&2; return 1
}

set_feature false
for _ in $(seq 1 30); do
  code="$(status "$base_url/subscribers" || true)"
  [[ "$code" == 503 ]] && break
  sleep 1
done
assert_status 503 "$base_url/subscribers"
assert_status 503 -X POST "$base_url/subscribers" -H 'Content-Type: application/json' -d '{"email":"demo@example.invalid"}'
set_feature true
registration="$(curl --max-time 5 -fsS -X POST "$base_url/subscribers" -H 'Content-Type: application/json' -d '{"email":"demo@example.invalid"}')"
subscriber_id="$(python3 -c 'import json,sys,uuid; print(uuid.UUID(json.load(sys.stdin)["id"]))' <<<"$registration")"
first_id="$(publish)"
wait_article "$first_id"
wait_notification "$first_id"

echo "Testing queued delivery across subscriber outage and feature disablement"
docker compose stop -t 5 subscriber-service >/dev/null
subscriber_stopped=1
queued_id="$(publish)"
wait_article "$queued_id"
wait_outbox_sent "$queued_id"
set_feature false
docker compose start subscriber-service >/dev/null
subscriber_stopped=0
sleep 5
assert_status 503 "$base_url/subscribers"
[[ "$(notifications "$queued_id")" == 0 ]] || { echo "Notification processed while disabled" >&2; exit 1; }
set_feature true
wait_notification "$queued_id"

echo "Testing moderation outage and circuit recovery"
comment_url="$base_url/articles/$first_id/comments"
original_comments="$(curl --max-time 5 -fsS "$comment_url" -H 'X-Region: eu')"
docker compose stop -t 5 profanity-service >/dev/null
profanity_stopped=1
for _ in 1 2 3; do
  assert_status 503 -X POST "$comment_url" -H 'X-Region: eu' -H 'Content-Type: application/json' -d '{"body":"Valid comment"}'
done
fast_result="$(curl --max-time 5 -sS -o /dev/null -w '%{http_code} %{time_total}' -X POST "$comment_url" -H 'X-Region: eu' -H 'Content-Type: application/json' -d '{"body":"Valid comment"}')"
python3 -c 'import sys; code,seconds=sys.stdin.read().split(); assert code == "503" and float(seconds) < 1.5, (code,seconds)' <<<"$fast_result"
assert_status 200 "$comment_url" -H 'X-Region: eu'
assert_status 200 "$base_url/articles/$first_id" -H 'X-Region: eu'
docker compose start profanity-service >/dev/null
profanity_stopped=0
recovered=0
for _ in $(seq 1 75); do
  code="$(status -X POST "$comment_url" -H 'X-Region: eu' -H 'Content-Type: application/json' -d '{"body":"A good recovery"}' || true)"
  if [[ "$code" == 201 ]]; then recovered=1; break; fi
  sleep 1
done
[[ "$recovered" == 1 ]] || { echo "Moderation circuit did not recover" >&2; exit 1; }
assert_status 400 -X POST "$comment_url" -H 'X-Region: eu' -H 'Content-Type: application/json' -d '{"body":"fuck"}'
final_comments="$(curl --max-time 5 -fsS "$comment_url" -H 'X-Region: eu')"
python3 -c 'import json,sys; before=json.loads(sys.argv[1]); after=json.load(sys.stdin); assert len(after) == len(before)+1 and all(x["body"] != "fuck" for x in after)' "$original_comments" <<<"$final_comments"
echo "PASS: feature gating, durable notification delivery, moderation failure isolation and recovery"
