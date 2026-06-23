using BurgerIAM.Shared.Events;
using BurgerIAM.TestUtilities;
using Grpc.Core;
using KitchenService.Data;
using KitchenService.Services;
using Microsoft.EntityFrameworkCore;
using ProtoCommon = BurgerIAM.Protos.Common;
using ProtoKitchen = BurgerIAM.Protos.Kitchen;

namespace KitchenService.Tests;

public class KitchenGrpcServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static KitchenGrpcService CreateService(AppDbContext db)
    {
        var eventBus = new BurgerIAM.TestUtilities.InMemoryEventBus();
        return new KitchenGrpcService(db, eventBus);
    }

    [Fact]
    public async Task GetPendingOrders_EmptyDb_ReturnsEmpty()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var response = await service.GetPendingOrders(new ProtoCommon.Empty(), new MockServerCallContext());
        Assert.Empty(response.Orders);
    }

    [Fact]
    public async Task GetPendingOrders_WithOrders_ReturnsPendingOnly()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.KitchenOrders.Add(new KitchenOrderEntity { OrderId = "order-1", Status = 0 });
        db.KitchenOrders.Add(new KitchenOrderEntity { OrderId = "order-2", Status = 1 });
        db.KitchenOrders.Add(new KitchenOrderEntity { OrderId = "order-3", Status = 2 });
        await db.SaveChangesAsync();
        var response = await service.GetPendingOrders(new ProtoCommon.Empty(), new MockServerCallContext());
        Assert.Equal(2, response.Orders.Count);
    }

    [Fact]
    public async Task StartPreparing_ExistingOrder_UpdatesStatus()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.KitchenOrders.Add(new KitchenOrderEntity { OrderId = "order-1", Status = 0 });
        await db.SaveChangesAsync();
        var response = await service.StartPreparing(new ProtoKitchen.StartPreparingRequest
        {
            OrderId = "order-1",
            Station = "Grill"
        }, new MockServerCallContext());
        Assert.Equal(1, response.Status);
        Assert.Equal("Grill", response.AssignedStation);
        Assert.NotEmpty(response.EstimatedReadyTime);
    }

    [Fact]
    public async Task StartPreparing_NonExistent_ThrowsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.StartPreparing(new ProtoKitchen.StartPreparingRequest { OrderId = "nonexistent", Station = "Grill" }, new MockServerCallContext()));
        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task StartPreparing_AlreadyInProgress_ThrowsFailedPrecondition()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.KitchenOrders.Add(new KitchenOrderEntity { OrderId = "order-1", Status = 1 });
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.StartPreparing(new ProtoKitchen.StartPreparingRequest { OrderId = "order-1", Station = "Grill" }, new MockServerCallContext()));
        Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
    }

    [Fact]
    public async Task MarkAsReady_ChangesStatus()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.KitchenOrders.Add(new KitchenOrderEntity { OrderId = "order-1", Status = 1 });
        await db.SaveChangesAsync();
        var response = await service.MarkAsReady(new ProtoKitchen.MarkAsReadyRequest { OrderId = "order-1" }, new MockServerCallContext());
        Assert.Equal(2, response.Status);
    }

    [Fact]
    public async Task MarkAsReady_NotInProgress_ThrowsFailedPrecondition()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.KitchenOrders.Add(new KitchenOrderEntity { OrderId = "order-1", Status = 0 });
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.MarkAsReady(new ProtoKitchen.MarkAsReadyRequest { OrderId = "order-1" }, new MockServerCallContext()));
        Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
    }

    [Fact]
    public async Task HandlePaymentConfirmed_CreatesKitchenOrder()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var paymentEvent = new PaymentConfirmedEvent
        {
            OrderId = "order-1",
            PaymentId = "pay-1",
            Amount = 15.99m
        };
        await service.HandlePaymentConfirmed(paymentEvent, CancellationToken.None);
        var kitchenOrder = await db.KitchenOrders.FirstOrDefaultAsync(k => k.OrderId == "order-1");
        Assert.NotNull(kitchenOrder);
        Assert.Equal(0, kitchenOrder.Status);
    }

    [Fact]
    public async Task HandlePaymentConfirmed_DuplicateOrder_DoesNotCreateDuplicate()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.KitchenOrders.Add(new KitchenOrderEntity { OrderId = "order-1", Status = 0 });
        await db.SaveChangesAsync();
        var paymentEvent = new PaymentConfirmedEvent
        {
            OrderId = "order-1",
            PaymentId = "pay-1",
            Amount = 15.99m
        };
        await service.HandlePaymentConfirmed(paymentEvent, CancellationToken.None);
        Assert.Single(db.KitchenOrders);
    }
}
