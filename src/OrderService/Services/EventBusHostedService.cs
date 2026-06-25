using BurgerIAM.EventBus;
using BurgerIAM.Shared.Events;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;

namespace OrderService.Services;

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
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var order = await db.Orders.FindAsync([@event.OrderId], cancellationToken: ct);
            if (order is null || order.Status >= 2)
                return;

            order.Status = 2;
            order.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
