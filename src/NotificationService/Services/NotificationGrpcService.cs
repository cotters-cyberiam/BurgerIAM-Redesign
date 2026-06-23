using BurgerIAM.EventBus;
using BurgerIAM.Shared.Events;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using ProtoCommon = BurgerIAM.Protos.Common;
using ProtoNotification = BurgerIAM.Protos.Notification;

namespace NotificationService.Services;

public sealed class NotificationGrpcService : ProtoNotification.NotificationService.NotificationServiceBase
{
    private readonly AppDbContext _db;
    private readonly IEventBus _eventBus;

    public NotificationGrpcService(AppDbContext db, IEventBus eventBus)
    {
        _db = db;
        _eventBus = eventBus;
    }

    public override async Task<ProtoNotification.GetNotificationsResponse> GetNotifications(
        ProtoNotification.GetNotificationsRequest request, ServerCallContext context)
    {
        var notifications = await _db.Notifications
            .Where(n => n.CustomerId == request.CustomerId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(context.CancellationToken);

        var response = new ProtoNotification.GetNotificationsResponse();
        response.Notifications.AddRange(notifications.Select(MapToProto));
        return response;
    }

    public override async Task<ProtoCommon.Empty> MarkAsRead(
        ProtoNotification.MarkAsReadRequest request, ServerCallContext context)
    {
        var notification = await _db.Notifications.FindAsync([request.NotificationId], cancellationToken: context.CancellationToken);

        if (notification is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Notification {request.NotificationId} not found"));

        notification.IsRead = true;
        await _db.SaveChangesAsync(context.CancellationToken);

        return new ProtoCommon.Empty();
    }

    public override async Task<ProtoNotification.UnreadCountResponse> GetUnreadCount(
        ProtoNotification.GetUnreadCountRequest request, ServerCallContext context)
    {
        var count = await _db.Notifications
            .CountAsync(n => n.CustomerId == request.CustomerId && !n.IsRead, context.CancellationToken);

        return new ProtoNotification.UnreadCountResponse { Count = count };
    }

    public async Task HandleOrderDelivered(OrderDeliveredEvent @event, CancellationToken cancellationToken)
    {
        var notification = new NotificationEntity
        {
            CustomerId = string.Empty,
            Title = "Order Delivered",
            Message = $"Your order {@event.OrderId} has been delivered. Enjoy your meal!"
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static ProtoNotification.Notification MapToProto(NotificationEntity entity)
    {
        return new ProtoNotification.Notification
        {
            Id = entity.Id,
            CustomerId = entity.CustomerId,
            Title = entity.Title,
            Message = entity.Message ?? string.Empty,
            IsRead = entity.IsRead,
            CreatedAt = entity.CreatedAt.ToString("O")
        };
    }
}
