using BurgerIAM.EventBus;
using BurgerIAM.Shared.Events;
using Microsoft.EntityFrameworkCore;
using ReceiptService.Data;

namespace ReceiptService.Services;

public sealed class ReceiptServiceHandler
{
    private readonly AppDbContext _db;
    private readonly IEventBus _eventBus;

    public ReceiptServiceHandler(AppDbContext db, IEventBus eventBus)
    {
        _db = db;
        _eventBus = eventBus;
    }

    public async Task HandlePaymentConfirmed(PaymentConfirmedEvent @event, CancellationToken cancellationToken)
    {
        var existing = await _db.Receipts.AnyAsync(r => r.OrderId == @event.OrderId, cancellationToken);
        if (existing) return;

        var receipt = new ReceiptEntity
        {
            OrderId = @event.OrderId,
            TotalAmount = (double)@event.Amount,
            ItemsJson = "[]"
        };

        _db.Receipts.Add(receipt);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReceiptEntity?> GetReceipt(string orderId)
    {
        return await _db.Receipts.FirstOrDefaultAsync(r => r.OrderId == orderId);
    }
}
