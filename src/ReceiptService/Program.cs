using BurgerIAM.EventBus;
using Microsoft.EntityFrameworkCore;
using ReceiptService;
using ReceiptService.Data;
using ReceiptService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var eventBusConnection = builder.Configuration.GetValue<string>("EventBus:ConnectionString");
if (string.IsNullOrWhiteSpace(eventBusConnection))
{
    builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
}
else
{
    var exchangeName = builder.Configuration.GetValue<string>("EventBus:ExchangeName") ?? "burgeriam.exchange";
    builder.Services.AddSingleton<IEventBus>(_ => new RabbitMQEventBus(eventBusConnection, exchangeName));
}

builder.Services.AddScoped<ReceiptServiceHandler>();
builder.Services.AddHostedService<EventBusHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/receipts/{orderId}", async (string orderId, ReceiptServiceHandler handler) =>
{
    var receipt = await handler.GetReceipt(orderId);
    if (receipt is null)
        return Results.NotFound(new { error = $"Receipt for order {orderId} not found" });

    var html = BuildReceiptHtml(receipt);

    return Results.Content(html, "text/html");
});

app.MapPost("/receipts", async (ReceiptServiceHandler handler, HttpContext http) =>
{
    var orderId = http.Request.Query["orderId"].FirstOrDefault();
    var customerId = http.Request.Query["customerId"].FirstOrDefault();
    var amountStr = http.Request.Query["amount"].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(customerId) || string.IsNullOrWhiteSpace(amountStr))
        return Results.BadRequest(new { error = "orderId, customerId, and amount query parameters are required" });

    if (!double.TryParse(amountStr, out var amount))
        return Results.BadRequest(new { error = "amount must be a valid number" });

    var existing = await handler.GetReceipt(orderId);
    if (existing is not null)
        return Results.Ok(new { receiptId = existing.Id, message = "Receipt already exists" });

    var receipt = new ReceiptEntity
    {
        OrderId = orderId,
        CustomerId = customerId,
        TotalAmount = amount,
        ItemsJson = "[]"
    };

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Receipts.Add(receipt);
    await db.SaveChangesAsync();

    return Results.Created($"/receipts/{orderId}", new { receiptId = receipt.Id });
});

static string BuildReceiptHtml(ReceiptEntity receipt)
{
    var sb = new System.Text.StringBuilder();
    var id = receipt.Id.Length > 8 ? receipt.Id[..8] : receipt.Id;
    var date = receipt.CreatedAt.ToString("yyyy-MM-dd HH:mm");
    sb.AppendLine("<!DOCTYPE html>");
    sb.AppendLine("<html lang=\"en\">");
    sb.AppendLine("<head>");
    sb.AppendLine("<meta charset=\"UTF-8\">");
    sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
    sb.AppendLine($"<title>Receipt - Order {receipt.OrderId}</title>");
    sb.AppendLine("<style>");
    sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 40px auto; padding: 20px; color: #333; }");
    sb.AppendLine(".header { text-align: center; border-bottom: 2px solid #e63946; padding-bottom: 15px; margin-bottom: 20px; }");
    sb.AppendLine(".header h1 { color: #e63946; margin: 0; font-size: 28px; }");
    sb.AppendLine(".header p { color: #666; margin: 5px 0 0; }");
    sb.AppendLine(".details { margin: 20px 0; }");
    sb.AppendLine(".row { display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #eee; }");
    sb.AppendLine(".label { color: #666; }");
    sb.AppendLine(".value { font-weight: 600; }");
    sb.AppendLine(".total { font-size: 20px; color: #e63946; text-align: right; margin-top: 20px; padding-top: 10px; border-top: 2px solid #e63946; }");
    sb.AppendLine(".footer { text-align: center; margin-top: 30px; color: #999; font-size: 12px; }");
    sb.AppendLine("</style>");
    sb.AppendLine("</head>");
    sb.AppendLine("<body>");
    sb.AppendLine("<div class=\"header\">");
    sb.AppendLine("<h1>BurgerIAM</h1>");
    sb.AppendLine("<p>Official Receipt</p>");
    sb.AppendLine("</div>");
    sb.AppendLine("<div class=\"details\">");
    sb.AppendLine($"<div class=\"row\"><span class=\"label\">Receipt #</span><span class=\"value\">{id}</span></div>");
    sb.AppendLine($"<div class=\"row\"><span class=\"label\">Order ID</span><span class=\"value\">{receipt.OrderId}</span></div>");
    sb.AppendLine($"<div class=\"row\"><span class=\"label\">Date</span><span class=\"value\">{date}</span></div>");
    sb.AppendLine("</div>");
    sb.AppendLine($"<div class=\"total\">Total: ${receipt.TotalAmount:F2}</div>");
    sb.AppendLine("<div class=\"footer\">");
    sb.AppendLine("<p>Thank you for your order!</p>");
    sb.AppendLine("<p>BurgerIAM - Fast Food Ordering System</p>");
    sb.AppendLine("</div>");
    sb.AppendLine("</body>");
    sb.AppendLine("</html>");
    return sb.ToString();
}

app.Run();
