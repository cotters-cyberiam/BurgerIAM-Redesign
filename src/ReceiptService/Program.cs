using Microsoft.EntityFrameworkCore;
using ReceiptService.Data;
using ReceiptService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ReceiptServiceHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/health/ready", async (AppDbContext db) =>
{
    try { await db.Database.CanConnectAsync(); return Results.Ok(new { status = "ready" }); }
    catch { return Results.StatusCode(503); }
});

app.MapGet("/receipts/{orderId}", async (string orderId, ReceiptServiceHandler handler) =>
{
    var receipt = await handler.GetReceipt(orderId);
    if (receipt is null)
        return Results.NotFound(new { error = $"Receipt for order {orderId} not found" });

    return Results.Ok(new
    {
        id = receipt.Id,
        orderId = receipt.OrderId,
        customerId = receipt.CustomerId,
        customerEmail = receipt.CustomerEmail,
        totalAmount = receipt.TotalAmount,
        itemsJson = receipt.ItemsJson,
        createdAt = receipt.CreatedAt
    });
});

app.MapPost("/receipts", async (ReceiptServiceHandler handler, CreateReceiptRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.OrderId) || string.IsNullOrWhiteSpace(request.CustomerId))
        return Results.BadRequest(new { error = "orderId and customerId are required" });

    var existing = await handler.GetReceipt(request.OrderId);
    if (existing is not null)
        return Results.Ok(new { receiptId = existing.Id, message = "Receipt already exists" });

    var receipt = new ReceiptEntity
    {
        OrderId = request.OrderId,
        CustomerId = request.CustomerId,
        CustomerEmail = request.CustomerEmail,
        TotalAmount = request.TotalAmount,
        ItemsJson = request.ItemsJson ?? "[]"
    };

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Receipts.Add(receipt);
    await db.SaveChangesAsync();

    return Results.Created($"/receipts/{request.OrderId}", new { receiptId = receipt.Id });
});

app.Run();

public record CreateReceiptRequest(string OrderId, string CustomerId, string? CustomerEmail, double TotalAmount, string? ItemsJson);
