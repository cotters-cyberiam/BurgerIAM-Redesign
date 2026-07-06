using BurgerIAM.EventBus;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
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

builder.Services.AddHostedService<EventBusHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapGrpcService<OrderGrpcService>();
app.MapGrpcReflectionService();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/health/ready", async (AppDbContext db) =>
{
    try { await db.Database.CanConnectAsync(); return Results.Ok(new { status = "ready" }); }
    catch { return Results.StatusCode(503); }
});

app.MapPost("/api/internal/orders/{id}/confirm-payment", async (string id, AppDbContext db) =>
{
    var order = await db.Orders.FindAsync(id);
    if (order is null)
        return Results.NotFound(new { error = $"Order {id} not found" });
    if (order.Status >= 2)
        return Results.Ok(new { status = order.Status });

    order.Status = 2;
    order.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new { status = order.Status });
});

app.MapPost("/api/internal/orders/{id}/status", async (string id, int status, AppDbContext db) =>
{
    var order = await db.Orders.FindAsync(id);
    if (order is null)
        return Results.NotFound(new { error = $"Order {id} not found" });
    if (order.Status >= status)
        return Results.Ok(new { status = order.Status });

    order.Status = status;
    order.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new { status = order.Status });
});

app.Run();
