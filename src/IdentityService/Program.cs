using IdentityService.Data;
using IdentityService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    db.Database.ExecuteSqlRaw(
        "CREATE TABLE IF NOT EXISTS TokenVersions (" +
        "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
        "Version TEXT NOT NULL, " +
        "GeneratedAt TEXT NOT NULL)");

    var tokenVersion = db.TokenVersions.FirstOrDefault();
    if (tokenVersion is null)
    {
        tokenVersion = new IdentityService.Data.TokenVersion
        {
            Version = Guid.NewGuid().ToString("N"),
            GeneratedAt = DateTime.UtcNow
        };
        db.TokenVersions.Add(tokenVersion);
    }
    else
    {
        tokenVersion.Version = Guid.NewGuid().ToString("N");
        tokenVersion.GeneratedAt = DateTime.UtcNow;
    }
    db.SaveChanges();
}

app.MapGrpcService<IdentityGrpcService>();
app.MapGrpcReflectionService();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/health/ready", async (AppDbContext db) =>
{
    try { await db.Database.CanConnectAsync(); return Results.Ok(new { status = "ready" }); }
    catch { return Results.StatusCode(503); }
});

app.Run();
