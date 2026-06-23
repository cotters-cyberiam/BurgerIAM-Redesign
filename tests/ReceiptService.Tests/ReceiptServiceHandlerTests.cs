using BurgerIAM.Shared.Events;
using BurgerIAM.TestUtilities;
using Microsoft.EntityFrameworkCore;
using ReceiptService.Data;
using ReceiptService.Services;

namespace ReceiptService.Tests;

public class ReceiptServiceHandlerTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ReceiptServiceHandler CreateHandler(AppDbContext db)
    {
        var eventBus = new InMemoryEventBus();
        return new ReceiptServiceHandler(db, eventBus);
    }

    [Fact]
    public async Task HandlePaymentConfirmed_CreatesReceipt()
    {
        var db = CreateDbContext();
        var handler = CreateHandler(db);
        var paymentEvent = new PaymentConfirmedEvent
        {
            OrderId = "order-1",
            PaymentId = "pay-1",
            Amount = 15.99m
        };
        await handler.HandlePaymentConfirmed(paymentEvent, CancellationToken.None);
        var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.OrderId == "order-1");
        Assert.NotNull(receipt);
        Assert.Equal(15.99, receipt.TotalAmount);
    }

    [Fact]
    public async Task HandlePaymentConfirmed_Duplicate_DoesNotCreateDuplicate()
    {
        var db = CreateDbContext();
        var handler = CreateHandler(db);
        db.Receipts.Add(new ReceiptEntity { OrderId = "order-1", CustomerId = "c1", TotalAmount = 10 });
        await db.SaveChangesAsync();
        var paymentEvent = new PaymentConfirmedEvent
        {
            OrderId = "order-1",
            PaymentId = "pay-1",
            Amount = 15.99m
        };
        await handler.HandlePaymentConfirmed(paymentEvent, CancellationToken.None);
        Assert.Single(db.Receipts);
    }

    [Fact]
    public async Task GetReceipt_Existing_ReturnsReceipt()
    {
        var db = CreateDbContext();
        var handler = CreateHandler(db);
        db.Receipts.Add(new ReceiptEntity { OrderId = "order-1", CustomerId = "c1", TotalAmount = 12.50 });
        await db.SaveChangesAsync();
        var receipt = await handler.GetReceipt("order-1");
        Assert.NotNull(receipt);
        Assert.Equal(12.50, receipt.TotalAmount);
    }

    [Fact]
    public async Task GetReceipt_NonExistent_ReturnsNull()
    {
        var db = CreateDbContext();
        var handler = CreateHandler(db);
        var receipt = await handler.GetReceipt("nonexistent");
        Assert.Null(receipt);
    }
}
