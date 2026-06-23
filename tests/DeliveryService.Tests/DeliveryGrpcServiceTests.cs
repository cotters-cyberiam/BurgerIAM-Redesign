using BurgerIAM.Shared.Events;
using BurgerIAM.TestUtilities;
using DeliveryService.Data;
using DeliveryService.Services;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ProtoDelivery = BurgerIAM.Protos.Delivery;

namespace DeliveryService.Tests;

public class DeliveryGrpcServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DeliveryGrpcService CreateService(AppDbContext db)
    {
        var eventBus = new BurgerIAM.TestUtilities.InMemoryEventBus();
        return new DeliveryGrpcService(db, eventBus);
    }

    [Fact]
    public async Task AssignDelivery_NoDrivers_ThrowsResourceExhausted()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.AssignDelivery(new ProtoDelivery.AssignDeliveryRequest
            {
                OrderId = "order-1",
                DeliveryAddress = "123 Main St"
            }, new MockServerCallContext()));
        Assert.Equal(StatusCode.ResourceExhausted, ex.StatusCode);
    }

    [Fact]
    public async Task AssignDelivery_WithAvailableDriver_ReturnsDelivery()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.Drivers.Add(new DriverEntity { Name = "Driver One", IsAvailable = true });
        await db.SaveChangesAsync();
        var response = await service.AssignDelivery(new ProtoDelivery.AssignDeliveryRequest
        {
            OrderId = "order-1",
            DeliveryAddress = "123 Main St"
        }, new MockServerCallContext());
        Assert.Equal("order-1", response.OrderId);
        Assert.Equal(1, response.Status);
        Assert.NotEmpty(response.DriverId);
        Assert.NotEmpty(response.DriverName);
    }

    [Fact]
    public async Task AssignDelivery_Duplicate_ReturnsExisting()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.Drivers.Add(new DriverEntity { Name = "Driver One", IsAvailable = true });
        await db.SaveChangesAsync();
        await service.AssignDelivery(new ProtoDelivery.AssignDeliveryRequest
        {
            OrderId = "order-1",
            DeliveryAddress = "123 Main St"
        }, new MockServerCallContext());
        var duplicate = await service.AssignDelivery(new ProtoDelivery.AssignDeliveryRequest
        {
            OrderId = "order-1",
            DeliveryAddress = "456 Oak Ave"
        }, new MockServerCallContext());
        Assert.Equal("order-1", duplicate.OrderId);
    }

    [Fact]
    public async Task UpdateDeliveryStatus_ToDelivered_UpdatesDriverAvailability()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var driver = new DriverEntity { Name = "Driver One", IsAvailable = true };
        db.Drivers.Add(driver);
        await db.SaveChangesAsync();
        var delivery = new DeliveryEntity
        {
            OrderId = "order-1",
            DriverId = driver.Id,
            DriverName = "Driver One",
            Status = 1,
            DeliveryAddress = "123 Main St"
        };
        db.Deliveries.Add(delivery);
        await db.SaveChangesAsync();
        var response = await service.UpdateDeliveryStatus(new ProtoDelivery.UpdateDeliveryStatusRequest
        {
            DeliveryId = delivery.Id,
            Status = 4
        }, new MockServerCallContext());
        Assert.Equal(4, response.Status);
        var updatedDriver = await db.Drivers.FindAsync(driver.Id);
        Assert.True(updatedDriver!.IsAvailable);
    }

    [Fact]
    public async Task GetDeliveryStatus_Existing_ReturnsDelivery()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var delivery = new DeliveryEntity
        {
            OrderId = "order-1",
            Status = 1,
            DeliveryAddress = "123 Main St"
        };
        db.Deliveries.Add(delivery);
        await db.SaveChangesAsync();
        var response = await service.GetDeliveryStatus(new ProtoDelivery.GetDeliveryRequest
        {
            OrderId = "order-1"
        }, new MockServerCallContext());
        Assert.Equal("order-1", response.OrderId);
        Assert.Equal(1, response.Status);
    }

    [Fact]
    public async Task GetDeliveryStatus_NonExistent_ThrowsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetDeliveryStatus(new ProtoDelivery.GetDeliveryRequest { OrderId = "nonexistent" }, new MockServerCallContext()));
        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task GetDriverDeliveries_ReturnsDriverDeliveries()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.Deliveries.Add(new DeliveryEntity { OrderId = "order-1", DriverId = "driver-1", Status = 1, DeliveryAddress = "Addr" });
        db.Deliveries.Add(new DeliveryEntity { OrderId = "order-2", DriverId = "driver-1", Status = 4, DeliveryAddress = "Addr" });
        db.Deliveries.Add(new DeliveryEntity { OrderId = "order-3", DriverId = "driver-2", Status = 1, DeliveryAddress = "Addr" });
        await db.SaveChangesAsync();
        var response = await service.GetDriverDeliveries(new ProtoDelivery.GetDriverDeliveriesRequest { DriverId = "driver-1" }, new MockServerCallContext());
        Assert.Equal(2, response.Deliveries.Count);
    }

    [Fact]
    public async Task HandleOrderReady_CreatesDeliveryRecord()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var orderReady = new OrderReadyEvent { OrderId = "order-1" };
        await service.HandleOrderReady(orderReady, CancellationToken.None);
        var delivery = await db.Deliveries.FirstOrDefaultAsync(d => d.OrderId == "order-1");
        Assert.NotNull(delivery);
        Assert.Equal(0, delivery.Status);
    }

    [Fact]
    public async Task HandleOrderReady_Duplicate_DoesNotCreateDuplicate()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.Deliveries.Add(new DeliveryEntity { OrderId = "order-1", Status = 0, DeliveryAddress = "" });
        await db.SaveChangesAsync();
        var orderReady = new OrderReadyEvent { OrderId = "order-1" };
        await service.HandleOrderReady(orderReady, CancellationToken.None);
        Assert.Single(db.Deliveries);
    }
}
