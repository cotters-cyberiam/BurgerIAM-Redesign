using BurgerIAM.Shared.Events;
using BurgerIAM.TestUtilities;

namespace BurgerIAM.EventBus.Tests;

public class EventBusTests
{
    [Fact]
    public async Task PublishAsync_DeliversEventToSubscriber()
    {
        var bus = new InMemoryEventBus();
        var delivered = new TaskCompletionSource<OrderPlacedEvent>();

        await bus.SubscribeAsync<OrderPlacedEvent>((@event, ct) =>
        {
            delivered.TrySetResult(@event);
            return Task.CompletedTask;
        });

        var orderEvent = new OrderPlacedEvent
        {
            OrderId = "order-1",
            CustomerId = "cust-1",
            CustomerEmail = "test@test.com",
            Items = [],
            TotalAmount = 10.00m,
            DeliveryAddress = "123 Main St"
        };

        await bus.PublishAsync(orderEvent);

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("order-1", result.OrderId);
    }

    [Fact]
    public async Task MultipleSubscribers_AllReceiveEvent()
    {
        var bus = new InMemoryEventBus();
        var count = 0;
        var semaphore = new SemaphoreSlim(0, 2);

        await bus.SubscribeAsync<PaymentConfirmedEvent>((@event, ct) =>
        {
            Interlocked.Increment(ref count);
            semaphore.Release();
            return Task.CompletedTask;
        });

        await bus.SubscribeAsync<PaymentConfirmedEvent>((@event, ct) =>
        {
            Interlocked.Increment(ref count);
            semaphore.Release();
            return Task.CompletedTask;
        });

        var paymentEvent = new PaymentConfirmedEvent
        {
            OrderId = "order-1",
            PaymentId = "pay-1",
            Amount = 15.00m
        };

        await bus.PublishAsync(paymentEvent);

        await semaphore.WaitAsync(TimeSpan.FromSeconds(5));
        await semaphore.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task PublishAsync_NoSubscribers_DoesNotThrow()
    {
        var bus = new InMemoryEventBus();

        var orderEvent = new OrderPlacedEvent
        {
            OrderId = "order-1",
            CustomerId = "cust-1",
            CustomerEmail = "test@test.com",
            Items = [],
            TotalAmount = 10.00m,
            DeliveryAddress = "123 Main St"
        };

        var exception = await Record.ExceptionAsync(() => bus.PublishAsync(orderEvent));
        Assert.Null(exception);
    }

    [Fact]
    public async Task Subscribe_ThenUnsubscribe_HandlerNotCalled()
    {
        var bus = new InMemoryEventBus();
        var called = false;

        async Task Handler(OrderCancelledEvent @event, CancellationToken ct)
        {
            called = true;
            await Task.CompletedTask;
        }

        await bus.SubscribeAsync<OrderCancelledEvent>(Handler);

        await bus.UnsubscribeAsync<OrderCancelledEvent>(Handler);

        await bus.PublishAsync(new OrderCancelledEvent
        {
            OrderId = "order-1",
            Reason = "Test"
        });

        Assert.False(called);
    }

    [Fact]
    public async Task DifferentEventTypes_OnlyRelevantHandlerCalled()
    {
        var bus = new InMemoryEventBus();
        var orderDelivered = false;
        var feedbackSubmitted = false;

        await bus.SubscribeAsync<OrderDeliveredEvent>((@event, ct) =>
        {
            orderDelivered = true;
            return Task.CompletedTask;
        });

        await bus.SubscribeAsync<FeedbackSubmittedEvent>((@event, ct) =>
        {
            feedbackSubmitted = true;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new OrderDeliveredEvent { OrderId = "order-1" });

        Assert.True(orderDelivered);
        Assert.False(feedbackSubmitted);
    }
}
