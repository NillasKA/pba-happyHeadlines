# Sustainability evidence

The [Green Architecture Framework](https://baerbak.cs.au.dk/papers/gaf.html) identifies tactics for reducing resource use, while emphasizing measurement and trade-offs. The [week 45 project requirement](https://moodle.easv.dk/mod/page/view.php?id=396024) asks for three tactics not previously worked on and before/after code. The following three tactics were absent from the baseline implementation and are introduced in this revision. Their measured effects remain unverified. No energy or carbon reduction has been measured.

## Defer Fetching/Lazy Fetch: paginated article lists

Previously, `ArticlesController.GetRecent` requested the complete recent list without pagination. The revised [controller](../../src/ArticleService/Controllers/ArticlesController.cs) accepts an optional limit of 1–100 and an offset; [ArticleRepository](../../src/ArticleService/Infrastructure/ArticleRepository.cs) applies them in the query. Omitting a limit preserves the unbounded behavior. A bounded request can transfer fewer records when the client needs only a page; later pages require additional requests.

Before:

```csharp
return Ok(await articleService.GetRecentAsync(region, cancellationToken));
```

After:

```csharp
return Ok(await articleService.GetRecentAsync(region, cancellationToken, limit, offset));
```

A screenshot can compare the complete list with `GET /articles?region=eu&limit=1&offset=0` using the same populated dataset, with observed record counts and response bytes. The list must have more than one article for this comparison to demonstrate a difference.

## Reduce Network Package Size: negotiated response compression

Previously ArticleService had no response-compression middleware. The revised [composition root](../../src/ArticleService/Program.cs) registers and enables it:

```csharp
builder.Services.AddResponseCompression();
// ...
app.UseResponseCompression();
```

A comparison of the same sufficiently large JSON response with and without `Accept-Encoding: gzip` can record the response encoding and transferred bytes. Compression consumes CPU and its effect depends on the response; smaller transferred size does not alone establish lower energy consumption.

## Reduce Bundle Size: smaller runtime base image

Previously the [ArticleService Dockerfile](../../src/ArticleService/Dockerfile) began with:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
```

The current runtime image begins with:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
```

The same runtime-image choice is used across application Dockerfiles. Pulled base-image sizes and the resulting application images require measurement and testing before a size improvement can be claimed; size alone does not prove a reduction in runtime energy. Image sizes vary by platform and image revision.

## Measurement status

`scripts/measure-gaf.sh` provides local response and base-image comparisons. It requires a running stack, a populated EU dataset and both base images available. Three legible images can show the command output or corresponding browser/network views. Each screenshot requires the tested revision, platform, dataset, request and result. No figures or screenshots have been recorded in this document.
