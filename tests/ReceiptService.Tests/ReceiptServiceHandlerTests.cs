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
        return new ReceiptServiceHandler(db);
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
