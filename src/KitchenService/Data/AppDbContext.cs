using Microsoft.EntityFrameworkCore;

namespace KitchenService.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<KitchenOrderEntity> KitchenOrders => Set<KitchenOrderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KitchenOrderEntity>(entity =>
        {
            entity.HasIndex(k => k.OrderId).IsUnique();
        });
    }
}
