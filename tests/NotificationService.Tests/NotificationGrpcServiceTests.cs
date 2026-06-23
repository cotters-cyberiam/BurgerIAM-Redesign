using BurgerIAM.Shared.Events;
using BurgerIAM.TestUtilities;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Services;
using ProtoCommon = BurgerIAM.Protos.Common;
using ProtoNotification = BurgerIAM.Protos.Notification;

namespace NotificationService.Tests;

public class NotificationGrpcServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static NotificationGrpcService CreateService(AppDbContext db)
    {
        var eventBus = new InMemoryEventBus();
        return new NotificationGrpcService(db, eventBus);
    }

    [Fact]
    public async Task GetNotifications_Empty_ReturnsEmpty()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var response = await service.GetNotifications(new ProtoNotification.GetNotificationsRequest { CustomerId = "customer-1" }, new MockServerCallContext());
        Assert.Empty(response.Notifications);
    }

    [Fact]
    public async Task GetNotifications_WithData_ReturnsCustomerNotifications()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.Notifications.Add(new NotificationEntity { CustomerId = "customer-1", Title = "Order Delivered", Message = "Msg 1" });
        db.Notifications.Add(new NotificationEntity { CustomerId = "customer-1", Title = "Feedback Thanks", Message = "Msg 2" });
        db.Notifications.Add(new NotificationEntity { CustomerId = "customer-2", Title = "Other", Message = "Msg 3" });
        await db.SaveChangesAsync();
        var response = await service.GetNotifications(new ProtoNotification.GetNotificationsRequest { CustomerId = "customer-1" }, new MockServerCallContext());
        Assert.Equal(2, response.Notifications.Count);
    }

    [Fact]
    public async Task MarkAsRead_Existing_UpdatesIsRead()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var entity = new NotificationEntity { CustomerId = "customer-1", Title = "Test", Message = "Test" };
        db.Notifications.Add(entity);
        await db.SaveChangesAsync();
        await service.MarkAsRead(new ProtoNotification.MarkAsReadRequest { NotificationId = entity.Id }, new MockServerCallContext());
        var updated = await db.Notifications.FindAsync(entity.Id);
        Assert.True(updated!.IsRead);
    }

    [Fact]
    public async Task MarkAsRead_NonExistent_ThrowsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.MarkAsRead(new ProtoNotification.MarkAsReadRequest { NotificationId = "nonexistent" }, new MockServerCallContext()));
        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsCorrectCount()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.Notifications.Add(new NotificationEntity { CustomerId = "customer-1", Title = "A", IsRead = false });
        db.Notifications.Add(new NotificationEntity { CustomerId = "customer-1", Title = "B", IsRead = true });
        db.Notifications.Add(new NotificationEntity { CustomerId = "customer-1", Title = "C", IsRead = false });
        await db.SaveChangesAsync();
        var response = await service.GetUnreadCount(new ProtoNotification.GetUnreadCountRequest { CustomerId = "customer-1" }, new MockServerCallContext());
        Assert.Equal(2, response.Count);
    }

    [Fact]
    public async Task HandleOrderDelivered_CreatesNotification()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var orderEvent = new OrderDeliveredEvent { OrderId = "order-1" };
        await service.HandleOrderDelivered(orderEvent, CancellationToken.None);
        var notifications = await db.Notifications.ToListAsync();
        Assert.Single(notifications);
        Assert.Contains("order-1", notifications[0].Message);
    }
}
