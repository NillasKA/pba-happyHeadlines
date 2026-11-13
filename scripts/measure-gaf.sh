#!/usr/bin/env bash
set -euo pipefail
base_url="${BASE_URL:-http://localhost:8080}"
region="${REGION:-eu}"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT
# Compare identical full-result representations with and without negotiation.
curl -fsS "$base_url/articles?region=$region" -H 'Accept-Encoding: identity' -o "$tmp_dir/full.json"
curl -fsS "$base_url/articles?region=$region" -H 'Accept-Encoding: gzip' -D "$tmp_dir/headers" -o "$tmp_dir/full.gz"
curl -fsS "$base_url/articles?region=$region&limit=1" -H 'Accept-Encoding: identity' -o "$tmp_dir/page.json"
python3 - "$tmp_dir" <<'PY'
import json, pathlib, sys
p = pathlib.Path(sys.argv[1])
full = json.loads((p / 'full.json').read_bytes())
page = json.loads((p / 'page.json').read_bytes())
print(f'Unbounded: {len(full)} articles, {(p / "full.json").stat().st_size} bytes')
print(f'Limit 1: {len(page)} articles, {(p / "page.json").stat().st_size} bytes')
print(f'Gzip response: {(p / "full.gz").stat().st_size} bytes')
print('Content-Encoding:', next((line for line in (p / 'headers').read_text().splitlines() if line.lower().startswith('content-encoding:')), 'not compressed'))
PY
printf 'Runtime image sizes (bytes):\n'
docker image inspect mcr.microsoft.com/dotnet/aspnet:10.0 mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra --format '{{.RepoTags}} {{.Size}}'
