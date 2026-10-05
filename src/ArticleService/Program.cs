using HappyHeadlines.ArticleService.Infrastructure;
using HappyHeadlines.ArticleService.Services;
using HappyHeadlines.ArticleService.Workers;
using HappyHeadlines.Shared;
using Npgsql;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("ArticleService");
builder.Services.AddControllers();
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

var postgresConnection = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=articles;Username=happy;Password=happy";
var redisConnection = builder.Configuration["Redis:Connection"] ?? "localhost:6379,abortConnect=false";
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(postgresConnection));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddSingleton<ArticleRepository>();
builder.Services.AddSingleton<ArticleCache>();
builder.Services.AddSingleton<StripedAsyncLock>();
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddHostedService<ArticlePersistenceConsumer>();

var app = builder.Build();
app.UseHappyHeadlinesMetrics("ArticleService");
app.MapControllers();
app.Run();
