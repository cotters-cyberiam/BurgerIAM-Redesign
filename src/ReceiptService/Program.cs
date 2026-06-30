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
    var date = receipt.CreatedAt.ToString("MMM dd, yyyy HH:mm");
    sb.AppendLine("<!DOCTYPE html>");
    sb.AppendLine("<html lang=\"en\">");
    sb.AppendLine("<head>");
    sb.AppendLine("<meta charset=\"UTF-8\">");
    sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
    sb.AppendLine($"<title>Receipt - Order {receipt.OrderId}</title>");
    sb.AppendLine("<style>");
    sb.AppendLine("@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap');");
    sb.AppendLine("*, *::before, *::after { box-sizing: border-box; }");
    sb.AppendLine("html { background: #0d0d1a; }");
    sb.AppendLine("body { font-family: 'Plus Jakarta Sans', 'Segoe UI', Arial, sans-serif; max-width: 520px; margin: 0 auto; padding: 32px 20px; background: #0d0d1a; color: #ffffff; min-height: 100vh; -webkit-font-smoothing: antialiased; }");
    sb.AppendLine(".header { text-align: center; padding-bottom: 20px; margin-bottom: 24px; border-bottom: 1px solid rgba(255,255,255,0.08); }");
    sb.AppendLine(".header h1 { margin: 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; background: linear-gradient(135deg, #e63946, #f4a261); -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text; }");
    sb.AppendLine(".header p { color: rgba(255,255,255,0.45); margin: 4px 0 0; font-size: 14px; }");
    sb.AppendLine(".card { background: rgba(255,255,255,0.04); border: 1px solid rgba(255,255,255,0.08); border-radius: 16px; padding: 20px; margin: 24px 0; }");
    sb.AppendLine(".row { display: flex; justify-content: space-between; align-items: center; padding: 12px 0; border-bottom: 1px solid rgba(255,255,255,0.06); }");
    sb.AppendLine(".row:last-child { border-bottom: none; }");
    sb.AppendLine(".label { color: rgba(255,255,255,0.5); font-size: 14px; font-weight: 500; }");
    sb.AppendLine(".value { color: #ffffff; font-weight: 600; font-size: 14px; }");
    sb.AppendLine(".total { text-align: right; margin-top: 24px; padding-top: 16px; border-top: 2px solid #e63946; }");
    sb.AppendLine(".total .label { font-size: 14px; color: rgba(255,255,255,0.5); margin-bottom: 4px; }");
    sb.AppendLine(".total .amount { font-size: 32px; font-weight: 800; color: #e63946; letter-spacing: -1px; }");
    sb.AppendLine(".footer { text-align: center; margin-top: 32px; padding-top: 20px; border-top: 1px solid rgba(255,255,255,0.06); }");
    sb.AppendLine(".footer p { margin: 4px 0; }");
    sb.AppendLine(".footer .brand { background: linear-gradient(135deg, #e63946, #f4a261); -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text; font-weight: 700; font-size: 16px; }");
    sb.AppendLine(".footer .small { color: rgba(255,255,255,0.35); font-size: 12px; }");
    sb.AppendLine("</style>");
    sb.AppendLine("</head>");
    sb.AppendLine("<body>");
    sb.AppendLine("<div class=\"header\">");
    sb.AppendLine("<h1>BurgerIAM</h1>");
    sb.AppendLine("<p>Official Receipt</p>");
    sb.AppendLine("</div>");
    sb.AppendLine("<div class=\"card\">");
    sb.AppendLine($"<div class=\"row\"><span class=\"label\">Receipt #</span><span class=\"value\">{id}</span></div>");
    sb.AppendLine($"<div class=\"row\"><span class=\"label\">Order ID</span><span class=\"value\">{receipt.OrderId}</span></div>");
    sb.AppendLine($"<div class=\"row\"><span class=\"label\">Date</span><span class=\"value\">{date}</span></div>");
    sb.AppendLine("</div>");
    sb.AppendLine("<div class=\"total\">");
    sb.AppendLine("<div class=\"label\">Total Amount</div>");
    sb.AppendLine($"<div class=\"amount\">&pound;{receipt.TotalAmount:F2}</div>");
    sb.AppendLine("</div>");
    sb.AppendLine("<div class=\"footer\">");
    sb.AppendLine("<p class=\"brand\">BurgerIAM</p>");
    sb.AppendLine("<p class=\"small\">Thank you for your order!</p>");
    sb.AppendLine("<p class=\"small\">Fast Food Ordering System</p>");
    sb.AppendLine("</div>");
    sb.AppendLine("</body>");
    sb.AppendLine("</html>");
    return sb.ToString();
}

app.Run();
