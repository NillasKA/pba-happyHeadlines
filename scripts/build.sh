#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

dotnet publish src/PublisherService/PublisherService.csproj -c Release -o artifacts/PublisherService /p:UseAppHost=false
dotnet publish src/NewsletterService/NewsletterService.csproj -c Release -o artifacts/NewsletterService /p:UseAppHost=false
dotnet publish src/ArticleService/ArticleService.csproj -c Release -o artifacts/ArticleService /p:UseAppHost=false
dotnet publish src/CommentService/CommentService.csproj -c Release -o artifacts/CommentService /p:UseAppHost=false
dotnet publish src/PrefillJob/PrefillJob.csproj -c Release -o artifacts/PrefillJob /p:UseAppHost=false
