using BurgerIAM.EventBus;
using BurgerIAM.TestUtilities;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Services;
using ProtoOrder = BurgerIAM.Protos.Order;

namespace OrderService.Tests;

public class OrderGrpcServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static OrderGrpcService CreateService(AppDbContext db)
    {
        var eventBus = new BurgerIAM.TestUtilities.InMemoryEventBus();
        return new OrderGrpcService(db, eventBus);
    }

    [Fact]
    public async Task CreateOrder_ReturnsOrderWithId()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var items = new List<ProtoOrder.OrderItem>
        {
            new() { MenuItemId = "item-1", ItemName = "Burger", Quantity = 2, UnitPrice = 5.99 }
        };

        var createRequest = new ProtoOrder.CreateOrderRequest
        {
            CustomerId = "cust-1",
            CustomerEmail = "test@test.com",
            DeliveryAddress = "123 Main St"
        };
        createRequest.Items.AddRange(items);

        var response = await service.CreateOrder(createRequest, new MockServerCallContext());

        Assert.False(string.IsNullOrWhiteSpace(response.Id));
        Assert.Equal(0, response.Status);
        Assert.Equal(11.98, response.TotalAmount);
        Assert.Single(response.Items);
    }

    [Fact]
    public async Task GetOrder_ExistingOrder_ReturnsOrder()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var createRequest = new ProtoOrder.CreateOrderRequest
        {
            CustomerId = "cust-1",
            CustomerEmail = "test@test.com",
            DeliveryAddress = "123 Main St"
        };
        createRequest.Items.Add(new ProtoOrder.OrderItem { MenuItemId = "item-1", ItemName = "Fries", Quantity = 1, UnitPrice = 2.50 });

        var created = await service.CreateOrder(createRequest, new MockServerCallContext());

        var response = await service.GetOrder(new ProtoOrder.GetOrderRequest { Id = created.Id }, new MockServerCallContext());

        Assert.Equal(created.Id, response.Id);
        Assert.Equal("cust-1", response.CustomerId);
    }

    [Fact]
    public async Task GetOrder_NonExistent_ThrowsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetOrder(new ProtoOrder.GetOrderRequest { Id = "nonexistent" }, new MockServerCallContext()));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task GetOrderStatus_ReturnsStatus()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var createRequest = new ProtoOrder.CreateOrderRequest
        {
            CustomerId = "cust-1",
            CustomerEmail = "test@test.com",
            DeliveryAddress = "123 Main St"
        };
        createRequest.Items.Add(new ProtoOrder.OrderItem { MenuItemId = "item-1", ItemName = "Soda", Quantity = 1, UnitPrice = 1.99 });

        var created = await service.CreateOrder(createRequest, new MockServerCallContext());

        var status = await service.GetOrderStatus(new ProtoOrder.GetOrderRequest { Id = created.Id }, new MockServerCallContext());

        Assert.Equal(created.Id, status.OrderId);
        Assert.Equal(0, status.Status);
    }

    [Fact]
    public async Task CancelOrder_ChangesStatusToCancelled()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var createRequest = new ProtoOrder.CreateOrderRequest
        {
            CustomerId = "cust-1",
            CustomerEmail = "test@test.com",
            DeliveryAddress = "123 Main St"
        };
        createRequest.Items.Add(new ProtoOrder.OrderItem { MenuItemId = "item-1", ItemName = "Burger", Quantity = 1, UnitPrice = 5.99 });

        var created = await service.CreateOrder(createRequest, new MockServerCallContext());

        var cancelled = await service.CancelOrder(new ProtoOrder.CancelOrderRequest
        {
            Id = created.Id,
            Reason = "Changed mind"
        }, new MockServerCallContext());

        Assert.Equal(7, cancelled.Status);
    }

    [Fact]
    public async Task CancelOrder_NonExistent_ThrowsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.CancelOrder(new ProtoOrder.CancelOrderRequest { Id = "nonexistent", Reason = "test" }, new MockServerCallContext()));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task GetCustomerOrders_ReturnsCustomerOrders()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var createRequest = new ProtoOrder.CreateOrderRequest
        {
            CustomerId = "cust-1",
            CustomerEmail = "test@test.com",
            DeliveryAddress = "123 Main St"
        };
        createRequest.Items.Add(new ProtoOrder.OrderItem { MenuItemId = "item-1", ItemName = "Burger", Quantity = 1, UnitPrice = 5.99 });

        await service.CreateOrder(createRequest, new MockServerCallContext());
        await service.CreateOrder(createRequest, new MockServerCallContext());

        var response = await service.GetCustomerOrders(new ProtoOrder.GetCustomerOrdersRequest { CustomerId = "cust-1" }, new MockServerCallContext());

        Assert.Equal(2, response.Orders.Count);
    }
}
