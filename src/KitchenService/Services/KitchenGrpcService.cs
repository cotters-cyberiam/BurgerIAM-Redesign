using BurgerIAM.EventBus;
using BurgerIAM.Shared.Events;
using Grpc.Core;
using KitchenService.Data;
using Microsoft.EntityFrameworkCore;
using ProtoCommon = BurgerIAM.Protos.Common;
using ProtoKitchen = BurgerIAM.Protos.Kitchen;

namespace KitchenService.Services;

public sealed class KitchenGrpcService : ProtoKitchen.KitchenService.KitchenServiceBase
{
    private readonly AppDbContext _db;
    private readonly IEventBus _eventBus;

    public KitchenGrpcService(AppDbContext db, IEventBus eventBus)
    {
        _db = db;
        _eventBus = eventBus;
    }

    public override async Task<ProtoKitchen.GetPendingOrdersResponse> GetPendingOrders(ProtoCommon.Empty request, ServerCallContext context)
    {
        var orders = await _db.KitchenOrders
            .Where(k => k.Status < 2)
            .OrderBy(k => k.CreatedAt)
            .ToListAsync(context.CancellationToken);

        var response = new ProtoKitchen.GetPendingOrdersResponse();
        response.Orders.AddRange(orders.Select(MapToProto));
        return response;
    }

    public override async Task<ProtoKitchen.KitchenOrder> StartPreparing(ProtoKitchen.StartPreparingRequest request, ServerCallContext context)
    {
        var kitchenOrder = await _db.KitchenOrders.FirstOrDefaultAsync(k => k.OrderId == request.OrderId, context.CancellationToken);

        if (kitchenOrder is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Kitchen order {request.OrderId} not found"));

        if (kitchenOrder.Status != 0)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Order already being prepared or completed"));

        kitchenOrder.Status = 1;
        kitchenOrder.AssignedStation = request.Station;
        kitchenOrder.EstimatedReadyTime = DateTime.UtcNow.AddMinutes(10);
        await _db.SaveChangesAsync(context.CancellationToken);

        await _eventBus.PublishAsync(new OrderInProgressEvent
        {
            OrderId = kitchenOrder.OrderId,
            EstimatedReadyTime = kitchenOrder.EstimatedReadyTime
        }, context.CancellationToken);

        return MapToProto(kitchenOrder);
    }

    public override async Task<ProtoKitchen.KitchenOrder> MarkAsReady(ProtoKitchen.MarkAsReadyRequest request, ServerCallContext context)
    {
        var kitchenOrder = await _db.KitchenOrders.FirstOrDefaultAsync(k => k.OrderId == request.OrderId, context.CancellationToken);

        if (kitchenOrder is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Kitchen order {request.OrderId} not found"));

        if (kitchenOrder.Status != 1)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Order must be in progress before marking as ready"));

        kitchenOrder.Status = 2;
        await _db.SaveChangesAsync(context.CancellationToken);

        await _eventBus.PublishAsync(new OrderReadyEvent
        {
            OrderId = kitchenOrder.OrderId
        }, context.CancellationToken);

        return MapToProto(kitchenOrder);
    }

    public override async Task<ProtoKitchen.KitchenOrder> GetKitchenOrder(ProtoKitchen.GetKitchenOrderRequest request, ServerCallContext context)
    {
        var kitchenOrder = await _db.KitchenOrders.FirstOrDefaultAsync(k => k.OrderId == request.OrderId, context.CancellationToken);

        if (kitchenOrder is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Kitchen order {request.OrderId} not found"));

        return MapToProto(kitchenOrder);
    }

    public async Task HandlePaymentConfirmed(PaymentConfirmedEvent @event, CancellationToken cancellationToken)
    {
        var existing = await _db.KitchenOrders.AnyAsync(k => k.OrderId == @event.OrderId, cancellationToken);
        if (existing) return;

        var kitchenOrder = new KitchenOrderEntity
        {
            OrderId = @event.OrderId,
            Status = 0
        };

        _db.KitchenOrders.Add(kitchenOrder);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static ProtoKitchen.KitchenOrder MapToProto(KitchenOrderEntity entity)
    {
        return new ProtoKitchen.KitchenOrder
        {
            Id = entity.Id,
            OrderId = entity.OrderId,
            Status = entity.Status,
            AssignedStation = entity.AssignedStation ?? string.Empty,
            EstimatedReadyTime = entity.EstimatedReadyTime?.ToString("O") ?? string.Empty,
            CreatedAt = entity.CreatedAt.ToString("O")
        };
    }
}
