using BurgerIAM.EventBus;
using BurgerIAM.Shared.Events;
using ReceiptService.Services;

namespace ReceiptService;

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
            var handler = scope.ServiceProvider.GetRequiredService<ReceiptServiceHandler>();
            await handler.HandlePaymentConfirmed(@event, ct);
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
