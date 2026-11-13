using HappyHeadlines.NewsletterService.Configuration;
using HappyHeadlines.NewsletterService.Infrastructure;
using HappyHeadlines.NewsletterService.Services;
using HappyHeadlines.NewsletterService.Workers;
using HappyHeadlines.Shared;
using Npgsql;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("NewsletterService");
builder.Services.Configure<NewsletterRabbitOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(builder.Configuration["Redis:Connection"] ?? "localhost:6379,abortConnect=false"));
builder.Services.AddSingleton<ISubscriberFeature, SubscriberFeature>();
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Port=5432;Database=newsletter;Username=happy;Password=happy"));
builder.Services.AddSingleton<ArticleStorePublisher>();
builder.Services.AddSingleton<IArticleStorePublisher>(services => services.GetRequiredService<ArticleStorePublisher>());
builder.Services.AddSingleton<ISubscriberOutbox, SubscriberOutbox>();
builder.Services.AddSingleton<ISubscriberPublisher, SubscriberPublisher>();
builder.Services.AddSingleton<OutboxDispatcher>();
builder.Services.AddScoped<INewsletterService, NewsletterService>();
builder.Services.AddHostedService<ArticlePublishedConsumer>();
builder.Services.AddHostedService<SubscriberOutboxWorker>();
var app = builder.Build();
app.UseHappyHeadlinesMetrics("NewsletterService");
app.MapGet("/features/subscribers", async (ISubscriberFeature feature, CancellationToken token) => Results.Ok(new { enabled = await feature.IsEnabledAsync(token) }));
app.MapPut("/features/subscribers", async (ISubscriberFeature feature, SubscriberFeatureRequest request, CancellationToken token) =>
{
    try { await feature.SetEnabledAsync(request.Enabled, token); return Results.Ok(new { enabled = request.Enabled }); }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
    catch (Exception) { return Results.Problem("Feature state is unavailable.", statusCode: 503); }
});
app.Run();
public sealed record SubscriberFeatureRequest(bool Enabled);
