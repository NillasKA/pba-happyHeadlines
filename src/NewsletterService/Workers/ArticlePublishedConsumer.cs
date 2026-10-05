using System.Diagnostics;
using System.Text.Json;
using HappyHeadlines.NewsletterService.Configuration;
using HappyHeadlines.NewsletterService.Services;
using HappyHeadlines.Shared;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace HappyHeadlines.NewsletterService.Workers;

public sealed class ArticlePublishedConsumer(
    IOptions<NewsletterRabbitOptions> optionsAccessor,
    IServiceScopeFactory scopeFactory,
    ILogger<ArticlePublishedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = optionsAccessor.Value;
        var factory = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            IConnection? connection = null;
            IModel? channel = null;
            try
            {
                connection = factory.CreateConnection("NewsletterService");
                channel = connection.CreateModel();
                channel.QueueDeclare(options.InputQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
                channel.BasicQos(0, prefetchCount: 1, global: false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, delivery) => await ProcessAsync(channel, delivery, stoppingToken);
                channel.BasicConsume(options.InputQueue, autoAck: false, consumerTag: "", noLocal: false,
                    exclusive: false, arguments: null, consumer: consumer);
                using (logger.Here())
                    logger.LogInformation("Consuming RabbitMQ queue {Queue}", options.InputQueue);
                while (!stoppingToken.IsCancellationRequested && connection.IsOpen)
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                using (logger.Here())
                    logger.LogWarning(exception, "RabbitMQ consumer unavailable; retrying in 3 seconds");
                try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
            finally
            {
                channel?.Dispose();
                connection?.Dispose();
            }
        }
    }

    private async Task ProcessAsync(IModel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        using var activity = TraceContext.Source.StartActivity(
            "article-queue.consume", ActivityKind.Consumer, TraceContext.Extract(delivery.BasicProperties.Headers));
        try
        {
            var article = JsonSerializer.Deserialize<ArticlePublished>(delivery.Body.ToArray(), JsonDefaults.Options)
                ?? throw new InvalidOperationException("Queue message did not contain an article.");
            activity?.SetTag("article.id", article.Id);
            activity?.SetTag("article.region", article.Region);

            await using var scope = scopeFactory.CreateAsyncScope();
            var newsletterService = scope.ServiceProvider.GetRequiredService<INewsletterService>();
            await newsletterService.ProcessAsync(article, stoppingToken);
            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            using (logger.Here())
                logger.LogError(exception, "Unable to process article queue message; it will be retried");
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: true);
        }
    }
}
