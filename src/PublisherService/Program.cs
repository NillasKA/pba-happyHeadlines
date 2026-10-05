using HappyHeadlines.PublisherService.Infrastructure;
using HappyHeadlines.PublisherService.Services;
using HappyHeadlines.Shared;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("PublisherService");
builder.Services.AddControllers();
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<ArticleEventPublisher>();
builder.Services.AddSingleton<IArticleEventPublisher>(services => services.GetRequiredService<ArticleEventPublisher>());
builder.Services.AddScoped<IPublisherService, PublisherService>();

var app = builder.Build();
app.UseHappyHeadlinesMetrics("PublisherService");
app.MapControllers();
app.Run();
