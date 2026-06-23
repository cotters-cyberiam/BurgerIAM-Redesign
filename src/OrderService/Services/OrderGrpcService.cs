using BurgerIAM.EventBus;
using BurgerIAM.Shared.DTOs;
using BurgerIAM.Shared.Events;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using ProtoCommon = BurgerIAM.Protos.Common;
using ProtoOrder = BurgerIAM.Protos.Order;

namespace OrderService.Services;

public sealed class OrderGrpcService : ProtoOrder.OrderService.OrderServiceBase
{
    private readonly AppDbContext _db;
    private readonly IEventBus _eventBus;

    public OrderGrpcService(AppDbContext db, IEventBus eventBus)
    {
        _db = db;
        _eventBus = eventBus;
    }

    public override async Task<ProtoOrder.Order> CreateOrder(ProtoOrder.CreateOrderRequest request, ServerCallContext context)
    {
        var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);

        var order = new OrderEntity
        {
            CustomerId = request.CustomerId,
            CustomerEmail = request.CustomerEmail,
            TotalAmount = (decimal)totalAmount,
            Status = 0,
            DeliveryAddress = request.DeliveryAddress,
            Items = request.Items.Select(i => new OrderItemEntity
            {
                MenuItemId = i.MenuItemId,
                ItemName = i.ItemName,
                Quantity = i.Quantity,
                UnitPrice = (decimal)i.UnitPrice
            }).ToList()
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(context.CancellationToken);

        await _eventBus.PublishAsync(new OrderPlacedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            CustomerEmail = order.CustomerEmail,
            Items = order.Items.Select(i => new OrderItemDto
            {
                MenuItemId = i.MenuItemId,
                ItemName = i.ItemName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList(),
            TotalAmount = order.TotalAmount,
            DeliveryAddress = order.DeliveryAddress
        }, context.CancellationToken);

        return MapToProto(order);
    }

    public override async Task<ProtoOrder.Order> GetOrder(ProtoOrder.GetOrderRequest request, ServerCallContext context)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.Id, context.CancellationToken);

        if (order is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Order {request.Id} not found"));

        return MapToProto(order);
    }

    public override async Task<ProtoOrder.OrderStatusResponse> GetOrderStatus(ProtoOrder.GetOrderRequest request, ServerCallContext context)
    {
        var order = await _db.Orders.FindAsync([request.Id], cancellationToken: context.CancellationToken);

        if (order is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Order {request.Id} not found"));

        return new ProtoOrder.OrderStatusResponse
        {
            OrderId = order.Id,
            Status = order.Status,
            UpdatedAt = order.UpdatedAt.ToString("O")
        };
    }

    public override async Task<ProtoOrder.Order> CancelOrder(ProtoOrder.CancelOrderRequest request, ServerCallContext context)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.Id, context.CancellationToken);

        if (order is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Order {request.Id} not found"));

        if (order.Status >= 2)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Cannot cancel order after payment"));

        order.Status = 7;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(context.CancellationToken);

        await _eventBus.PublishAsync(new OrderCancelledEvent
        {
            OrderId = order.Id,
            Reason = request.Reason
        }, context.CancellationToken);

        return MapToProto(order);
    }

    public override async Task<ProtoOrder.GetCustomerOrdersResponse> GetCustomerOrders(ProtoOrder.GetCustomerOrdersRequest request, ServerCallContext context)
    {
        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerId == request.CustomerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(context.CancellationToken);

        var response = new ProtoOrder.GetCustomerOrdersResponse();
        response.Orders.AddRange(orders.Select(MapToProto));
        return response;
    }

    private static ProtoOrder.Order MapToProto(OrderEntity order)
    {
        var proto = new ProtoOrder.Order
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            CustomerEmail = order.CustomerEmail,
            TotalAmount = (double)order.TotalAmount,
            Status = order.Status,
            DeliveryAddress = order.DeliveryAddress,
            CreatedAt = order.CreatedAt.ToString("O"),
            UpdatedAt = order.UpdatedAt.ToString("O")
        };

        proto.Items.AddRange(order.Items.Select(i => new ProtoOrder.OrderItem
        {
            MenuItemId = i.MenuItemId,
            ItemName = i.ItemName,
            Quantity = i.Quantity,
            UnitPrice = (double)i.UnitPrice
        }));

        return proto;
    }
}
