using Microsoft.EntityFrameworkCore;
using ReceiptService.Data;

namespace ReceiptService.Services;

public sealed class ReceiptServiceHandler
{
    private readonly AppDbContext _db;

    public ReceiptServiceHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ReceiptEntity?> GetReceipt(string orderId)
    {
        return await _db.Receipts.FirstOrDefaultAsync(r => r.OrderId == orderId);
    }
}
