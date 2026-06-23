using BurgerIAM.EventBus;
using BurgerIAM.Shared.Events;
using BurgerIAM.TestUtilities;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Services;
using ProtoPayment = BurgerIAM.Protos.Payment;

namespace PaymentService.Tests;

public class PaymentGrpcServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static PaymentGrpcService CreateService(AppDbContext db, IEventBus? eventBus = null)
    {
        eventBus ??= new BurgerIAM.TestUtilities.InMemoryEventBus();
        return new PaymentGrpcService(db, eventBus);
    }

    [Fact]
    public async Task ProcessPayment_ReturnsPaymentId()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var response = await service.ProcessPayment(new ProtoPayment.ProcessPaymentRequest
        {
            OrderId = "order-1",
            CustomerId = "cust-1",
            Amount = 15.99,
            Method = "CreditCard"
        }, new MockServerCallContext());

        Assert.False(string.IsNullOrWhiteSpace(response.PaymentId));
        Assert.Equal(2, response.Status);
    }

    [Fact]
    public async Task ProcessPayment_DuplicateOrder_ReturnsExisting()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        await service.ProcessPayment(new ProtoPayment.ProcessPaymentRequest
        {
            OrderId = "order-1",
            CustomerId = "cust-1",
            Amount = 10.00,
            Method = "CreditCard"
        }, new MockServerCallContext());

        var duplicate = await service.ProcessPayment(new ProtoPayment.ProcessPaymentRequest
        {
            OrderId = "order-1",
            CustomerId = "cust-1",
            Amount = 10.00,
            Method = "CreditCard"
        }, new MockServerCallContext());

        Assert.NotEmpty(duplicate.PaymentId);
    }

    [Fact]
    public async Task GetPayment_ExistingPayment_ReturnsPayment()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var processed = await service.ProcessPayment(new ProtoPayment.ProcessPaymentRequest
        {
            OrderId = "order-1",
            CustomerId = "cust-1",
            Amount = 10.00,
            Method = "DebitCard"
        }, new MockServerCallContext());

        var payment = await service.GetPayment(new ProtoPayment.GetPaymentRequest
        {
            PaymentId = processed.PaymentId
        }, new MockServerCallContext());

        Assert.Equal("order-1", payment.OrderId);
        Assert.Equal("DebitCard", payment.Method);
    }

    [Fact]
    public async Task GetPayment_NonExistent_ThrowsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetPayment(new ProtoPayment.GetPaymentRequest { PaymentId = "nonexistent" }, new MockServerCallContext()));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task RefundPayment_ChangesStatusToRefunded()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var processed = await service.ProcessPayment(new ProtoPayment.ProcessPaymentRequest
        {
            OrderId = "order-1",
            CustomerId = "cust-1",
            Amount = 10.00,
            Method = "CreditCard"
        }, new MockServerCallContext());

        var refunded = await service.RefundPayment(new ProtoPayment.RefundPaymentRequest
        {
            PaymentId = processed.PaymentId,
            Reason = "Customer request"
        }, new MockServerCallContext());

        Assert.Equal(4, refunded.Status);
    }

    [Fact]
    public async Task HandleOrderPlaced_CreatesPayment()
    {
        var db = CreateDbContext();
        var eventBus = new BurgerIAM.TestUtilities.InMemoryEventBus();
        var service = CreateService(db, eventBus);
        var paymentCreated = new TaskCompletionSource<PaymentConfirmedEvent>();

        await eventBus.SubscribeAsync<PaymentConfirmedEvent>(async (@event, ct) =>
        {
            paymentCreated.TrySetResult(@event);
            await Task.CompletedTask;
        });

        var placedEvent = new OrderPlacedEvent
        {
            OrderId = "order-1",
            CustomerId = "cust-1",
            CustomerEmail = "test@test.com",
            Items = [],
            TotalAmount = 15.99m,
            DeliveryAddress = "123 Main St"
        };

        await service.HandleOrderPlaced(placedEvent, CancellationToken.None);

        var confirmed = await paymentCreated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("order-1", confirmed.OrderId);
        Assert.Equal(15.99m, confirmed.Amount);
    }
}
