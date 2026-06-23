using Microsoft.EntityFrameworkCore;

namespace NotificationService.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.HasIndex(n => n.CustomerId);
        });
    }
}
