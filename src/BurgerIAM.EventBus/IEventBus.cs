using BurgerIAM.Shared.Events;

namespace BurgerIAM.EventBus;

public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IntegrationEvent;

    Task SubscribeAsync<T>(Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default) where T : IntegrationEvent;

    Task UnsubscribeAsync<T>(Func<T, CancellationToken, Task> handler) where T : IntegrationEvent;
}
