using System.Collections.Concurrent;
using BurgerIAM.EventBus;
using BurgerIAM.Shared.Events;

namespace BurgerIAM.EventBus.Tests;

public sealed class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<string, List<Delegate>> _handlers = new();

    public Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IntegrationEvent
    {
        var eventType = typeof(T).Name;

        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            foreach (var handler in handlers.Cast<Func<T, CancellationToken, Task>>())
            {
                handler(@event, cancellationToken);
            }
        }

        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default) where T : IntegrationEvent
    {
        var eventType = typeof(T).Name;

        _handlers.AddOrUpdate(
            eventType,
            _ => [handler],
            (_, list) => { list.Add(handler); return list; });

        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync<T>(Func<T, CancellationToken, Task> handler) where T : IntegrationEvent
    {
        var eventType = typeof(T).Name;

        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            handlers.Remove(handler);
        }

        return Task.CompletedTask;
    }
}
