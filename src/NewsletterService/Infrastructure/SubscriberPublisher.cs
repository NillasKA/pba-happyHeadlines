using System.Text;
using System.Text.Json;
using HappyHeadlines.NewsletterService.Configuration;
using HappyHeadlines.Shared;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace HappyHeadlines.NewsletterService.Infrastructure;

public interface ISubscriberPublisher
{
    Task PublishAsync(ArticlePublished article, CancellationToken cancellationToken);
}

public sealed class SubscriberPublisher(IOptions<NewsletterRabbitOptions> optionsAccessor) : ISubscriberPublisher
{
    public Task PublishAsync(ArticlePublished article, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = optionsAccessor.Value;
        var factory = new ConnectionFactory { HostName = options.Host, Port = options.Port, UserName = options.UserName,
            Password = options.Password, RequestedConnectionTimeout = TimeSpan.FromSeconds(2) };
        using var connection = factory.CreateConnection("NewsletterService-subscriber-publisher");
        using var channel = connection.CreateModel();
        channel.QueueDeclare(options.SubscriberQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        channel.ConfirmSelect();
        var properties = channel.CreateBasicProperties();
        properties.ContentType = "application/json";
        properties.DeliveryMode = 2;
        properties.MessageId = article.Id.ToString();
        properties.Headers = TraceContext.Inject(System.Diagnostics.Activity.Current);
        channel.BasicPublish("", options.SubscriberQueue, false, properties,
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(article, JsonDefaults.Options)));
        channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(2));
        return Task.CompletedTask;
    }
}
