using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BurgerIAM.Shared.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BurgerIAM.EventBus;

public sealed class RabbitMQEventBus : IEventBus, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _exchangeName;
    private readonly ConcurrentDictionary<string, List<EventSubscription>> _handlers = new();
    private readonly ConcurrentDictionary<string, string> _consumerTags = new();
    private bool _disposed;

    public RabbitMQEventBus(string connectionString, string exchangeName = "burgeriam.exchange")
    {
        _exchangeName = exchangeName;
        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        _channel.ExchangeDeclareAsync(
            exchange: _exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false).GetAwaiter().GetResult();
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IntegrationEvent
    {
        var routingKey = @event.EventType;
        var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType());

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = @event.EventId,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _channel.BasicPublishAsync(
            exchange: _exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    public async Task SubscribeAsync<T>(Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default) where T : IntegrationEvent
    {
        var eventType = typeof(T).Name;

        var subscription = new EventSubscription
        {
            EventType = typeof(T),
            Handler = handler
        };

        _handlers.AddOrUpdate(
            eventType,
            _ => [subscription],
            (_, list) => { list.Add(subscription); return list; });

        var queueName = $"{eventType}.queue";
        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: _exchangeName,
            routingKey: eventType,
            cancellationToken: cancellationToken);

        if (!_consumerTags.ContainsKey(eventType))
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, args) =>
            {
                var body = args.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);

                if (_handlers.TryGetValue(args.RoutingKey, out var subscriptions))
                {
                    foreach (var sub in subscriptions)
                    {
                        var eventData = JsonSerializer.Deserialize(json, sub.EventType) as T;
                        if (eventData is not null)
                        {
                            await ((Func<T, CancellationToken, Task>)sub.Handler)(eventData, cancellationToken);
                        }
                    }
                }
            };

            var tag = await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: true,
                consumer: consumer,
                cancellationToken: cancellationToken);

            _consumerTags.TryAdd(eventType, tag);
        }
    }

    public Task UnsubscribeAsync<T>(Func<T, CancellationToken, Task> handler) where T : IntegrationEvent
    {
        var eventType = typeof(T).Name;

        if (_handlers.TryGetValue(eventType, out var subscriptions))
        {
            subscriptions.RemoveAll(s =>
                s.Handler.Equals(handler));
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_channel is not null)
            await _channel.CloseAsync();

        if (_connection is not null)
            await _connection.CloseAsync();
    }
}
