using HappyHeadlines.PrefillJob.Infrastructure;
using HappyHeadlines.PrefillJob.Services;
using HappyHeadlines.PrefillJob.Workers;
using HappyHeadlines.Shared;
using Npgsql;
using Serilog;
using Serilog.Formatting.Json;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
var seqUrl = builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341";
builder.Services.AddSerilog((_, logger) => logger
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "ArticlePrefillJob")
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.Seq(seqUrl));

var postgresConnection = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=articles;Username=happy;Password=happy";
var redisConnection = builder.Configuration["Redis:Connection"] ?? "localhost:6379,abortConnect=false";
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(postgresConnection));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddSingleton<IArticlePrefillRepository, ArticlePrefillRepository>();
builder.Services.AddSingleton<IArticleCacheWriter, ArticleCacheWriter>();
builder.Services.AddSingleton<IPrefillService, PrefillService>();
builder.Services.AddHostedService<ArticlePrefillWorker>();

await builder.Build().RunAsync();
