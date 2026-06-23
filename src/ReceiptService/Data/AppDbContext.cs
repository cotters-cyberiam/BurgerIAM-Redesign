using Microsoft.EntityFrameworkCore;

namespace ReceiptService.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ReceiptEntity> Receipts => Set<ReceiptEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReceiptEntity>(entity =>
        {
            entity.HasIndex(r => r.OrderId).IsUnique();
        });
    }
}
