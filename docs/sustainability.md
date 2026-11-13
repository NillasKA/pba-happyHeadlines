# Sustainability evidence

The [Green Architecture Framework](https://baerbak.cs.au.dk/papers/gaf.html) identifies tactics for reducing resource use, while emphasizing measurement and trade-offs. The following three tactics were absent from the baseline implementation and are introduced in this revision. The effects below were measured in [verification run 43422690611](https://github.com/NillasKA/pba-happyHeadlines/actions/runs/43422690611) on Linux x64. No energy or carbon reduction was measured.

## Defer Fetching/Lazy Fetch: paginated article lists

Previously, `ArticlesController.GetRecent` requested the complete recent list without pagination. The revised [controller](../src/ArticleService/Controllers/ArticlesController.cs) accepts an optional limit of 1–100 and an offset; [ArticleRepository](../src/ArticleService/Infrastructure/ArticleRepository.cs) applies them in the query. Omitting a limit preserves the unbounded behavior. A bounded request can transfer fewer records when the client needs only a page; later pages require additional requests.

Before:

```csharp
return Ok(await articleService.GetRecentAsync(region, cancellationToken));
```

After:

```csharp
return Ok(await articleService.GetRecentAsync(region, cancellationToken, limit, offset));
```

The [comparison](evidence/gaf-pagination.png) returned six articles and 5,600 bytes without a limit, versus one article and 1,681 bytes with `limit=1` on the same dataset.

## Reduce Network Package Size: negotiated response compression

Previously ArticleService had no response-compression middleware. The revised [composition root](../src/ArticleService/Program.cs) registers and enables it:

```csharp
builder.Services.AddResponseCompression();
// ...
app.UseResponseCompression();
```

The [comparison](evidence/gaf-compression.png) transferred 627 bytes with gzip versus 5,600 bytes for the same uncompressed response. Compression consumes CPU and its effect depends on the response; smaller transferred size does not alone establish lower energy consumption.

## Reduce Bundle Size: smaller runtime base image

Previously the [ArticleService Dockerfile](../src/ArticleService/Dockerfile) began with:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
```

The current runtime image begins with:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
```

The same runtime-image choice is used across application Dockerfiles. The [comparison](evidence/gaf-image.png) measured 97,675,516 bytes for the full base image and 72,585,216 bytes for the chiseled base image on Linux x64. Image sizes vary by platform and revision; size alone does not prove a reduction in runtime energy.

## Measurement status

The [measurement log](evidence/gaf-measurements.log) records the comparisons made by `scripts/measure-gaf.sh`; the [manifest](evidence/manifest.json) records revision `7a0fc14b0bfaafaffa815c7f9a3125bae595c744`, Linux x64 and capture time 2026-11-13 12:11:27 UTC. The dataset consisted of synthetic articles in the local Compose stack.
