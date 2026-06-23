using BurgerIAM.EventBus;
using FeedbackService.Data;
using FeedbackService.Services;
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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapGrpcService<FeedbackGrpcService>();
app.MapGrpcReflectionService();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
