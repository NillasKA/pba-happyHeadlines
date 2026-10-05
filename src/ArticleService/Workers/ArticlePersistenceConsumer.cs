using System.Diagnostics;
using System.Text.Json;
using HappyHeadlines.ArticleService.Services;
using HappyHeadlines.Shared;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace HappyHeadlines.ArticleService.Workers;

public sealed class ArticlePersistenceConsumer(
    IOptions<RabbitMqOptions> optionsAccessor,
    IArticleService articleService,
    ILogger<ArticlePersistenceConsumer> logger) : BackgroundService
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
                connection = factory.CreateConnection("ArticleService");
                channel = connection.CreateModel();
                channel.QueueDeclare(options.Queue, durable: true, exclusive: false, autoDelete: false, arguments: null);
                channel.BasicQos(0, prefetchCount: 1, global: false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, delivery) => await ProcessAsync(channel, delivery, stoppingToken);
                channel.BasicConsume(options.Queue, autoAck: false, consumerTag: "", noLocal: false,
                    exclusive: false, arguments: null, consumer: consumer);
                using (logger.Here())
                    logger.LogInformation("Consuming RabbitMQ article store queue {Queue}", options.Queue);
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
                    logger.LogWarning(exception, "RabbitMQ article consumer unavailable; retrying in 3 seconds");
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
            "article.persist", ActivityKind.Consumer, TraceContext.Extract(delivery.BasicProperties.Headers));
        try
        {
            var article = JsonSerializer.Deserialize<ArticlePublished>(delivery.Body.ToArray(), JsonDefaults.Options)
                ?? throw new InvalidOperationException("Article store message was empty.");
            article = article with { Region = Regions.Normalize(article.Region) };
            activity?.SetTag("article.id", article.Id);
            activity?.SetTag("article.region", article.Region);

            await articleService.PersistPublishedAsync(article, stoppingToken);
            HappyMetrics.ConsumedEvents.Inc();
            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            using (logger.Here())
                logger.LogError(exception, "Article persistence failed; requeueing message");
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: true);
        }
    }
}
