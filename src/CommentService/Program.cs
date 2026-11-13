using HappyHeadlines.CommentService.Infrastructure;
using HappyHeadlines.CommentService.Services;
using HappyHeadlines.Shared;
using Npgsql;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("CommentService");
builder.Services.AddControllers();

var postgresConnection = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=comments;Username=happy;Password=happy";
var redisConnection = builder.Configuration["Redis:Connection"] ?? "localhost:6379,abortConnect=false";
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(postgresConnection));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddSingleton<CommentRepository>();
builder.Services.AddSingleton<CommentCache>();
builder.Services.AddSingleton<StripedAsyncLock>();
builder.Services.AddHttpClient<ProfanityClient>(client => client.BaseAddress = new Uri(builder.Configuration["Profanity:BaseUrl"] ?? "http://localhost:8080"))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });
builder.Services.AddSingleton<IProfanityClient>(services => services.GetRequiredService<ProfanityClient>());
builder.Services.AddScoped<ICommentService, CommentService>();

var app = builder.Build();
app.UseHappyHeadlinesMetrics("CommentService");
app.MapControllers();
app.Run();
