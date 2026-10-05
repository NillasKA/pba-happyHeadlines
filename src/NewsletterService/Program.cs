using HappyHeadlines.NewsletterService.Configuration;
using HappyHeadlines.NewsletterService.Infrastructure;
using HappyHeadlines.NewsletterService.Services;
using HappyHeadlines.NewsletterService.Workers;
using HappyHeadlines.Shared;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("NewsletterService");
builder.Services.Configure<NewsletterRabbitOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<ArticleStorePublisher>();
builder.Services.AddSingleton<IArticleStorePublisher>(services => services.GetRequiredService<ArticleStorePublisher>());
builder.Services.AddScoped<INewsletterService, NewsletterService>();
builder.Services.AddHostedService<ArticlePublishedConsumer>();

var app = builder.Build();
app.UseHappyHeadlinesMetrics("NewsletterService");
app.Run();
