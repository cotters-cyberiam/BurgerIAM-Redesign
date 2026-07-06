using BurgerIAM.EventBus;
using DeliveryService;
using DeliveryService.Data;
using DeliveryService.Services;
using Microsoft.EntityFrameworkCore;

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
    if (!db.Drivers.Any())
    {
        db.Drivers.Add(new DriverEntity { Name = "Default Driver", IsAvailable = true });
        db.SaveChanges();
    }
}

app.MapGrpcService<DeliveryGrpcService>();
app.MapGrpcReflectionService();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/health/ready", async (AppDbContext db) =>
{
    try { await db.Database.CanConnectAsync(); return Results.Ok(new { status = "ready" }); }
    catch { return Results.StatusCode(503); }
});

app.Run();
