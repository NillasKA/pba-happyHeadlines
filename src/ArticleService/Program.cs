using HappyHeadlines.ArticleService.Infrastructure;
using HappyHeadlines.ArticleService.Services;
using HappyHeadlines.ArticleService.Workers;
using HappyHeadlines.Shared;
using Npgsql;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("ArticleService");
builder.Services.AddControllers();
builder.Services.AddResponseCompression();
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

var redisConnection = builder.Configuration["Redis:Connection"] ?? "localhost:6379,abortConnect=false";
builder.Services.AddSingleton<ArticleShardRouter>();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddSingleton<ArticleRepository>();
builder.Services.AddSingleton<ArticleCache>();
builder.Services.AddSingleton<StripedAsyncLock>();
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddHostedService<ArticlePersistenceConsumer>();

var app = builder.Build();
app.UseResponseCompression();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Article-Instance"] = Environment.MachineName;
    await next();
});
app.UseHappyHeadlinesMetrics("ArticleService");
app.MapControllers();
app.Run();
