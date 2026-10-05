using System.Text;
using System.Text.Json;
using HappyHeadlines.NewsletterService.Configuration;
using HappyHeadlines.Shared;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace HappyHeadlines.NewsletterService.Infrastructure;

public sealed class ArticleStorePublisher : IArticleStorePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly object _gate = new();
    private readonly string _queue;

    public ArticleStorePublisher(IOptions<NewsletterRabbitOptions> optionsAccessor)
    {
        var options = optionsAccessor.Value;
        _queue = options.OutputQueue;
        var factory = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };
        _connection = factory.CreateConnection("NewsletterService-article-store-publisher");
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(_queue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        _channel.ConfirmSelect();
    }

    public Task PublishAsync(ArticlePublished article, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(article, JsonDefaults.Options));
        var properties = _channel.CreateBasicProperties();
        properties.ContentType = "application/json";
        properties.DeliveryMode = 2;
        properties.MessageId = article.Id.ToString();
        properties.Headers = TraceContext.Inject(System.Diagnostics.Activity.Current);

        lock (_gate)
        {
            _channel.BasicPublish(exchange: "", routingKey: _queue, mandatory: false, basicProperties: properties, body: body);
            _channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }
}
