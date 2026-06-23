using Microsoft.EntityFrameworkCore;

namespace FeedbackService.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<FeedbackEntity> FeedbackEntries => Set<FeedbackEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeedbackEntity>(entity =>
        {
            entity.HasIndex(f => f.OrderId).IsUnique();
        });
    }
}
