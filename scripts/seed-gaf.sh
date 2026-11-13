#!/usr/bin/env bash
set -euo pipefail
base_url="${BASE_URL:-http://localhost:8080}"
for i in 1 2 3; do
  payload="$(python3 -c 'import json,sys; print(json.dumps({"title": "Measurement article " + sys.argv[1], "body": "A repeatable measurement of article response size. " * 30, "region": "eu"}))' "$i")"
  result="$(curl -fsS -X POST "$base_url/publish" -H 'Content-Type: application/json' -d "$payload")"
  id="$(python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])' <<<"$result")"
  ready=0
  for _ in $(seq 1 45); do
    if curl -fsS "$base_url/articles/$id" -H 'X-Region: eu' >/dev/null 2>&1; then ready=1; break; fi
    sleep 1
  done
  if [[ "$ready" != 1 ]]; then
    echo "Timed out waiting for measurement article $id" >&2
    exit 1
  fi
done
