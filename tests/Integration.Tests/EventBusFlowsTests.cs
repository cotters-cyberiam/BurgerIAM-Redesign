using BurgerIAM.Shared.Events;
using BurgerIAM.TestUtilities;

namespace Integration.Tests;

public class EventBusFlowsTests
{
    [Fact]
    public async Task OrderPlaced_TriggersPaymentProcessing()
    {
        var bus = new InMemoryEventBus();
        var paymentTriggered = new TaskCompletionSource<OrderPlacedEvent>();

        await bus.SubscribeAsync<OrderPlacedEvent>(async (@event, ct) =>
        {
            paymentTriggered.TrySetResult(@event);
            await Task.CompletedTask;
        });

        var orderEvent = new OrderPlacedEvent
        {
            OrderId = "order-1",
            CustomerId = "cust-1",
            CustomerEmail = "test@test.com",
            Items = [],
            TotalAmount = 15.99m,
            DeliveryAddress = "123 Main St"
        };

        await bus.PublishAsync(orderEvent);
        var received = await paymentTriggered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("order-1", received.OrderId);
        Assert.Equal(15.99m, received.TotalAmount);
    }

    [Fact]
    public async Task PaymentConfirmed_TriggersKitchenAndReceipt()
    {
        var bus = new InMemoryEventBus();
        var kitchenNotified = new TaskCompletionSource<PaymentConfirmedEvent>();
        var receiptGenerated = new TaskCompletionSource<PaymentConfirmedEvent>();

        await bus.SubscribeAsync<PaymentConfirmedEvent>(async (@event, ct) =>
        {
            kitchenNotified.TrySetResult(@event);
            await Task.CompletedTask;
        });

        await bus.SubscribeAsync<PaymentConfirmedEvent>(async (@event, ct) =>
        {
            receiptGenerated.TrySetResult(@event);
            await Task.CompletedTask;
        });

        var paymentEvent = new PaymentConfirmedEvent
        {
            OrderId = "order-1",
            PaymentId = "pay-1",
            Amount = 15.99m
        };

        await bus.PublishAsync(paymentEvent);

        var kitchenResult = await kitchenNotified.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var receiptResult = await receiptGenerated.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("order-1", kitchenResult.OrderId);
        Assert.Equal("order-1", receiptResult.OrderId);
    }

    [Fact]
    public async Task PaymentFailed_DoesNotTriggerKitchen()
    {
        var bus = new InMemoryEventBus();
        var kitchenCalled = false;

        await bus.SubscribeAsync<PaymentConfirmedEvent>(async (@event, ct) =>
        {
            kitchenCalled = true;
            await Task.CompletedTask;
        });

        await bus.PublishAsync(new PaymentFailedEvent
        {
            OrderId = "order-1",
            PaymentId = "pay-1",
            Reason = "Insufficient funds"
        });

        Assert.False(kitchenCalled);
    }

    [Fact]
    public async Task OrderLifecycle_FullEventChain()
    {
        var bus = new InMemoryEventBus();
        var eventsReceived = new List<string>();

        await bus.SubscribeAsync<OrderPlacedEvent>(async (@event, ct) =>
        {
            lock (eventsReceived) eventsReceived.Add(nameof(OrderPlacedEvent));

            var paymentConfirmed = new PaymentConfirmedEvent
            {
                OrderId = @event.OrderId,
                PaymentId = $"pay-{@event.OrderId}",
                Amount = @event.TotalAmount
            };
            await bus.PublishAsync(paymentConfirmed);
        });

        await bus.SubscribeAsync<PaymentConfirmedEvent>(async (@event, ct) =>
        {
            lock (eventsReceived) eventsReceived.Add(nameof(PaymentConfirmedEvent));

            var orderInProgress = new OrderInProgressEvent
            {
                OrderId = @event.OrderId,
                EstimatedReadyTime = DateTime.UtcNow.AddMinutes(10)
            };
            await bus.PublishAsync(orderInProgress);
        });

        await bus.SubscribeAsync<OrderInProgressEvent>(async (@event, ct) =>
        {
            lock (eventsReceived) eventsReceived.Add(nameof(OrderInProgressEvent));

            var orderReady = new OrderReadyEvent
            {
                OrderId = @event.OrderId
            };
            await bus.PublishAsync(orderReady);
        });

        await bus.SubscribeAsync<OrderReadyEvent>(async (@event, ct) =>
        {
            lock (eventsReceived) eventsReceived.Add(nameof(OrderReadyEvent));

            var outForDelivery = new OrderOutForDeliveryEvent
            {
                OrderId = @event.OrderId,
                DriverId = "driver-1",
                EstimatedDeliveryTime = DateTime.UtcNow.AddMinutes(20)
            };
            await bus.PublishAsync(outForDelivery);
        });

        await bus.SubscribeAsync<OrderOutForDeliveryEvent>(async (@event, ct) =>
        {
            lock (eventsReceived) eventsReceived.Add(nameof(OrderOutForDeliveryEvent));

            var delivered = new OrderDeliveredEvent
            {
                OrderId = @event.OrderId
            };
            await bus.PublishAsync(delivered);
        });

        await bus.SubscribeAsync<OrderDeliveredEvent>(async (@event, ct) =>
        {
            lock (eventsReceived) eventsReceived.Add(nameof(OrderDeliveredEvent));
            await Task.CompletedTask;
        });

        await bus.PublishAsync(new OrderPlacedEvent
        {
            OrderId = "order-1",
            CustomerId = "cust-1",
            CustomerEmail = "test@test.com",
            Items = [],
            TotalAmount = 10.00m,
            DeliveryAddress = "123 Main St"
        });

        await Task.Delay(500);

        Assert.Contains(nameof(OrderPlacedEvent), eventsReceived);
        Assert.Contains(nameof(PaymentConfirmedEvent), eventsReceived);
        Assert.Contains(nameof(OrderInProgressEvent), eventsReceived);
        Assert.Contains(nameof(OrderReadyEvent), eventsReceived);
        Assert.Contains(nameof(OrderOutForDeliveryEvent), eventsReceived);
        Assert.Contains(nameof(OrderDeliveredEvent), eventsReceived);
        Assert.Equal(6, eventsReceived.Count);
    }
}
