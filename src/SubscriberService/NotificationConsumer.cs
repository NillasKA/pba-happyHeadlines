using System.Diagnostics;
using System.Text.Json;
using HappyHeadlines.Shared;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace HappyHeadlines.SubscriberService;

public sealed class SubscriberRabbitOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string SubscriberQueue { get; set; } = "subscriber.notifications";
}

public sealed class NotificationConsumer(IOptions<SubscriberRabbitOptions> optionsAccessor,
    SubscriberRepository repository, ISubscriberFeature feature, ILogger<NotificationConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = optionsAccessor.Value;
        var factory = new ConnectionFactory { HostName = options.Host, Port = options.Port, UserName = options.UserName,
            Password = options.Password, DispatchConsumersAsync = true, AutomaticRecoveryEnabled = true };
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (!await feature.IsEnabledAsync(stoppingToken))
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                using var connection = factory.CreateConnection("SubscriberService");
                using var channel = connection.CreateModel();
                channel.QueueDeclare(options.SubscriberQueue, true, false, false, null);
                channel.BasicQos(0, 1, false);
                var consumer = new AsyncEventingBasicConsumer(channel);
                var paused = 0;
                consumer.Received += async (_, delivery) =>
                {
                    using var activity = TraceContext.Source.StartActivity("subscriber-queue.consume", ActivityKind.Consumer,
                        TraceContext.Extract(delivery.BasicProperties.Headers));
                    try
                    {
                        if (!await feature.IsEnabledAsync(stoppingToken))
                        {
                            channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: true);
                            channel.BasicCancel(delivery.ConsumerTag);
                            Interlocked.Exchange(ref paused, 1);
                            return;
                        }
                        if (!SubscriberEventParser.TryParse(delivery.Body, out var article))
                        {
                            logger.LogWarning("Discarding malformed subscriber event");
                            HappyMetrics.SubscriberEvents.WithLabels("invalid").Inc();
                            channel.BasicReject(delivery.DeliveryTag, requeue: false);
                            return;
                        }

                        activity?.SetTag("article.id", article.Id);
                        var count = await repository.RecordNotificationsAsync(article.Id, stoppingToken);
                        HappyMetrics.SubscriberEvents.WithLabels(count == 0 ? "duplicate_or_empty" : "processed").Inc();
                        logger.LogInformation("Recorded notification work for article {ArticleId} and {Count} subscribers", article.Id, count);
                        channel.BasicAck(delivery.DeliveryTag, false);
                    }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogWarning(exception, "Subscriber event failed; retrying");
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                        channel.BasicNack(delivery.DeliveryTag, false, true);
                    }
                };
                channel.BasicConsume(options.SubscriberQueue, false, consumer);
                while (!stoppingToken.IsCancellationRequested && connection.IsOpen && channel.IsOpen && Volatile.Read(ref paused) == 0)
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Subscriber consumer unavailable; retrying");
                try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
