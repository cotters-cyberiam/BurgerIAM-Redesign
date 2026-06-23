using BurgerIAM.EventBus;
using BurgerIAM.Shared.Events;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using ProtoPayment = BurgerIAM.Protos.Payment;

namespace PaymentService.Services;

public sealed class PaymentGrpcService : ProtoPayment.PaymentService.PaymentServiceBase
{
    private readonly AppDbContext _db;
    private readonly IEventBus _eventBus;

    public PaymentGrpcService(AppDbContext db, IEventBus eventBus)
    {
        _db = db;
        _eventBus = eventBus;
    }

    public override async Task<ProtoPayment.PaymentResponse> ProcessPayment(ProtoPayment.ProcessPaymentRequest request, ServerCallContext context)
    {
        var existing = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == request.OrderId, context.CancellationToken);
        if (existing is not null)
        {
            return new ProtoPayment.PaymentResponse
            {
                PaymentId = existing.Id,
                OrderId = existing.OrderId,
                Status = existing.Status,
                Error = existing.Status != 2 ? "Payment already exists" : string.Empty
            };
        }

        var payment = new PaymentEntity
        {
            OrderId = request.OrderId,
            Amount = (decimal)request.Amount,
            Status = 2,
            Method = request.Method
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(context.CancellationToken);

        await _eventBus.PublishAsync(new PaymentConfirmedEvent
        {
            OrderId = payment.OrderId,
            PaymentId = payment.Id,
            Amount = payment.Amount
        }, context.CancellationToken);

        return new ProtoPayment.PaymentResponse
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            Status = payment.Status
        };
    }

    public override async Task<ProtoPayment.Payment> GetPayment(ProtoPayment.GetPaymentRequest request, ServerCallContext context)
    {
        var payment = await _db.Payments.FindAsync([request.PaymentId], cancellationToken: context.CancellationToken);

        if (payment is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Payment {request.PaymentId} not found"));

        return MapToProto(payment);
    }

    public override async Task<ProtoPayment.PaymentResponse> RefundPayment(ProtoPayment.RefundPaymentRequest request, ServerCallContext context)
    {
        var payment = await _db.Payments.FindAsync([request.PaymentId], cancellationToken: context.CancellationToken);

        if (payment is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Payment {request.PaymentId} not found"));

        if (payment.Status != 2)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Only confirmed payments can be refunded"));

        payment.Status = 4;
        await _db.SaveChangesAsync(context.CancellationToken);

        await _eventBus.PublishAsync(new PaymentFailedEvent
        {
            OrderId = payment.OrderId,
            PaymentId = payment.Id,
            Amount = payment.Amount,
            Reason = request.Reason
        }, context.CancellationToken);

        return new ProtoPayment.PaymentResponse
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            Status = payment.Status
        };
    }

    public async Task HandleOrderPlaced(OrderPlacedEvent @event, CancellationToken cancellationToken)
    {
        var existing = await _db.Payments.AnyAsync(p => p.OrderId == @event.OrderId, cancellationToken);
        if (existing) return;

        var payment = new PaymentEntity
        {
            OrderId = @event.OrderId,
            Amount = @event.TotalAmount,
            Status = 2,
            Method = "CreditCard"
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new PaymentConfirmedEvent
        {
            OrderId = payment.OrderId,
            PaymentId = payment.Id,
            Amount = payment.Amount
        }, cancellationToken);
    }

    private static ProtoPayment.Payment MapToProto(PaymentEntity payment)
    {
        return new ProtoPayment.Payment
        {
            Id = payment.Id,
            OrderId = payment.OrderId,
            Amount = (double)payment.Amount,
            Status = payment.Status,
            Method = payment.Method,
            CreatedAt = payment.CreatedAt.ToString("O")
        };
    }
}
