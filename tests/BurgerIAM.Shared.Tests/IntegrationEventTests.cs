using BurgerIAM.Shared.Events;

namespace BurgerIAM.Shared.Tests;

public class IntegrationEventTests
{
    [Fact]
    public void IntegrationEvent_AssignsEventId()
    {
        var sut = new TestEvent();
        Assert.False(string.IsNullOrWhiteSpace(sut.EventId));
    }

    [Fact]
    public void IntegrationEvent_AssignsOccurredOn()
    {
        var sut = new TestEvent();
        Assert.True(sut.OccurredOn <= DateTime.UtcNow);
    }

    [Fact]
    public void IntegrationEvent_EventType_ReturnsClassName()
    {
        var sut = new TestEvent();
        Assert.Equal(nameof(TestEvent), sut.EventType);
    }

    [Fact]
    public void OrderPlacedEvent_HasAllProperties()
    {
        var items = new List<DTOs.OrderItemDto>
        {
            new() { MenuItemId = "1", ItemName = "Burger", Quantity = 2, UnitPrice = 5.99m }
        };

        var sut = new OrderPlacedEvent
        {
            OrderId = "order-1",
            CustomerId = "cust-1",
            CustomerEmail = "test@test.com",
            Items = items,
            TotalAmount = 11.98m,
            DeliveryAddress = "123 Main St"
        };

        Assert.Equal("order-1", sut.OrderId);
        Assert.Equal("cust-1", sut.CustomerId);
        Assert.Equal("test@test.com", sut.CustomerEmail);
        Assert.Single(sut.Items);
        Assert.Equal(11.98m, sut.TotalAmount);
        Assert.Equal("123 Main St", sut.DeliveryAddress);
    }

    [Fact]
    public void PaymentConfirmedEvent_HasAllProperties()
    {
        var sut = new PaymentConfirmedEvent
        {
            OrderId = "order-1",
            PaymentId = "pay-1",
            Amount = 11.98m
        };

        Assert.Equal("order-1", sut.OrderId);
        Assert.Equal("pay-1", sut.PaymentId);
        Assert.Equal(11.98m, sut.Amount);
    }

    [Fact]
    public void OrderCancelledEvent_IncludesReason()
    {
        var sut = new OrderCancelledEvent
        {
            OrderId = "order-1",
            Reason = "Customer request"
        };

        Assert.Equal("order-1", sut.OrderId);
        Assert.Equal("Customer request", sut.Reason);
    }

    private sealed record TestEvent : IntegrationEvent;
}
