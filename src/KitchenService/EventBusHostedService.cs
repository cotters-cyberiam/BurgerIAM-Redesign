using BurgerIAM.EventBus;
using BurgerIAM.Shared.Events;
using KitchenService.Services;

namespace KitchenService;

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
        await _eventBus.SubscribeAsync<PaymentConfirmedEvent>(async (@event, ct) =>
        {
            using var scope = _scopeFactory.CreateScope();
            var kitchenService = scope.ServiceProvider.GetRequiredService<KitchenGrpcService>();
            await kitchenService.HandlePaymentConfirmed(@event, ct);
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
