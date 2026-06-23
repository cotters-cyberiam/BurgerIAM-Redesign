using BurgerIAM.EventBus;
using BurgerIAM.Shared.Events;
using NotificationService.Services;

namespace NotificationService;

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
        await _eventBus.SubscribeAsync<OrderDeliveredEvent>(async (@event, ct) =>
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<NotificationGrpcService>();
            await service.HandleOrderDelivered(@event, ct);
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
