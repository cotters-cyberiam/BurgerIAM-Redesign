using Microsoft.EntityFrameworkCore;

namespace DeliveryService.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DeliveryEntity> Deliveries => Set<DeliveryEntity>();
    public DbSet<DriverEntity> Drivers => Set<DriverEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeliveryEntity>(entity =>
        {
            entity.HasIndex(d => d.OrderId).IsUnique();
        });
    }
}
