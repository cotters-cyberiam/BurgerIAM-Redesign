using BurgerIAM.EventBus;
using BurgerIAM.Shared.Events;
using DeliveryService.Data;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ProtoDelivery = BurgerIAM.Protos.Delivery;

namespace DeliveryService.Services;

public sealed class DeliveryGrpcService : ProtoDelivery.DeliveryService.DeliveryServiceBase
{
    private readonly AppDbContext _db;
    private readonly IEventBus _eventBus;

    public DeliveryGrpcService(AppDbContext db, IEventBus eventBus)
    {
        _db = db;
        _eventBus = eventBus;
    }

    public override async Task<ProtoDelivery.Delivery> AssignDelivery(ProtoDelivery.AssignDeliveryRequest request, ServerCallContext context)
    {
        var existing = await _db.Deliveries.FirstOrDefaultAsync(d => d.OrderId == request.OrderId, context.CancellationToken);
        if (existing is not null)
        {
            return MapToProto(existing);
        }

        var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.IsAvailable, context.CancellationToken);
        if (driver is null)
            throw new RpcException(new Status(StatusCode.ResourceExhausted, "No available drivers"));

        var delivery = new DeliveryEntity
        {
            OrderId = request.OrderId,
            DriverId = driver.Id,
            DriverName = driver.Name,
            Status = 1,
            DeliveryAddress = request.DeliveryAddress,
            EstimatedDeliveryTime = DateTime.UtcNow.AddMinutes(20)
        };

        driver.IsAvailable = false;
        _db.Deliveries.Add(delivery);
        await _db.SaveChangesAsync(context.CancellationToken);

        await _eventBus.PublishAsync(new OrderOutForDeliveryEvent
        {
            OrderId = delivery.OrderId,
            DriverId = driver.Id,
            EstimatedDeliveryTime = delivery.EstimatedDeliveryTime
        }, context.CancellationToken);

        return MapToProto(delivery);
    }

    public override async Task<ProtoDelivery.Delivery> UpdateDeliveryStatus(ProtoDelivery.UpdateDeliveryStatusRequest request, ServerCallContext context)
    {
        var delivery = await _db.Deliveries.FindAsync([request.DeliveryId], cancellationToken: context.CancellationToken);

        if (delivery is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Delivery {request.DeliveryId} not found"));

        delivery.Status = request.Status;

        if (request.Status == 4)
        {
            delivery.CompletedAt = DateTime.UtcNow;
            if (delivery.DriverId is not null)
            {
                var driver = await _db.Drivers.FindAsync([delivery.DriverId], cancellationToken: context.CancellationToken);
                if (driver is not null)
                    driver.IsAvailable = true;
            }
        }

        await _db.SaveChangesAsync(context.CancellationToken);

        if (request.Status == 4)
        {
            await _eventBus.PublishAsync(new OrderDeliveredEvent
            {
                OrderId = delivery.OrderId
            }, context.CancellationToken);
        }

        return MapToProto(delivery);
    }

    public override async Task<ProtoDelivery.Delivery> GetDeliveryStatus(ProtoDelivery.GetDeliveryRequest request, ServerCallContext context)
    {
        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.OrderId == request.OrderId, context.CancellationToken);

        if (delivery is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Delivery for order {request.OrderId} not found"));

        return MapToProto(delivery);
    }

    public override async Task<ProtoDelivery.GetDeliveriesResponse> GetDriverDeliveries(ProtoDelivery.GetDriverDeliveriesRequest request, ServerCallContext context)
    {
        var deliveries = await _db.Deliveries
            .Where(d => d.DriverId == request.DriverId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(context.CancellationToken);

        var response = new ProtoDelivery.GetDeliveriesResponse();
        response.Deliveries.AddRange(deliveries.Select(MapToProto));
        return response;
    }

    public async Task HandleOrderReady(OrderReadyEvent @event, CancellationToken cancellationToken)
    {
        var existing = await _db.Deliveries.AnyAsync(d => d.OrderId == @event.OrderId, cancellationToken);
        if (existing) return;

        var delivery = new DeliveryEntity
        {
            OrderId = @event.OrderId,
            Status = 0,
            DeliveryAddress = string.Empty
        };

        _db.Deliveries.Add(delivery);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static ProtoDelivery.Delivery MapToProto(DeliveryEntity entity)
    {
        return new ProtoDelivery.Delivery
        {
            Id = entity.Id,
            OrderId = entity.OrderId,
            DriverId = entity.DriverId ?? string.Empty,
            DriverName = entity.DriverName ?? string.Empty,
            Status = entity.Status,
            DeliveryAddress = entity.DeliveryAddress,
            EstimatedDeliveryTime = entity.EstimatedDeliveryTime?.ToString("O") ?? string.Empty,
            CompletedAt = entity.CompletedAt?.ToString("O") ?? string.Empty
        };
    }
}
