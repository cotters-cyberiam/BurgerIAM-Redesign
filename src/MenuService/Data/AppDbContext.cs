using Microsoft.EntityFrameworkCore;

namespace MenuService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<MenuItemEntity> MenuItems => Set<MenuItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MenuItemEntity>(entity =>
        {
            entity.HasIndex(m => m.Category);
        });
    }
}
