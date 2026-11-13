using HappyHeadlines.Shared;
using HappyHeadlines.SubscriberService;
using Npgsql;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("SubscriberService");
builder.Services.Configure<SubscriberRabbitOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=subscribers;Username=happy;Password=happy"));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(builder.Configuration["Redis:Connection"] ?? "localhost:6379,abortConnect=false"));
builder.Services.AddSingleton<ISubscriberFeature, SubscriberFeature>();
builder.Services.AddSingleton<SubscriberRepository>();
builder.Services.AddHostedService<NotificationConsumer>();
var app = builder.Build();
app.UseHappyHeadlinesMetrics("SubscriberService");
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/subscribers") &&
        !await context.RequestServices.GetRequiredService<ISubscriberFeature>().IsEnabledAsync(context.RequestAborted))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { error = "Subscriber interactions are disabled or unavailable." }, context.RequestAborted);
        return;
    }
    await next();
});
app.MapPost("/subscribers", async (RegisterSubscriber request, SubscriberRepository repository, CancellationToken token) =>
{
    var email = request.Email?.Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(email) || !System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email)
        return Results.BadRequest(new { error = "a valid email is required" });
    var subscriber = await repository.RegisterAsync(email, token);
    return Results.Ok(subscriber);
});
app.MapGet("/subscribers", async (SubscriberRepository repository, CancellationToken token) =>
    Results.Ok(await repository.ListAsync(token)));
app.Run();

public sealed record RegisterSubscriber(string? Email);
