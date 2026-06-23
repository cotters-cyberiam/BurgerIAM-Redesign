using BurgerIAM.EventBus;
using BurgerIAM.Shared.Events;
using DeliveryService.Services;

namespace DeliveryService;

public sealed class EventBusHostedService : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly IServiceScopeFactory _scopeFactory;

    public EventBusHostedService(IEventBus eventBus, IServiceScopeFactory scopeFactory)
    {
        _eventBus = eventBus;
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _eventBus.SubscribeAsync<OrderReadyEvent>(async (@event, ct) =>
        {
            using var scope = _scopeFactory.CreateScope();
            var deliveryService = scope.ServiceProvider.GetRequiredService<DeliveryGrpcService>();
            await deliveryService.HandleOrderReady(@event, ct);
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
