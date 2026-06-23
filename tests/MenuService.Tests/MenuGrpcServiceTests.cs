using BurgerIAM.TestUtilities;
using BurgerIAM.Protos.Common;
using Grpc.Core;
using MenuService.Data;
using MenuService.Services;
using Microsoft.EntityFrameworkCore;

namespace MenuService.Tests;

public class MenuGrpcServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static MenuGrpcService CreateService(AppDbContext db)
    {
        return new MenuGrpcService(db);
    }

    [Fact]
    public async Task GetMenuItems_EmptyDatabase_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var response = await service.GetMenuItems(new Empty(), new MockServerCallContext());

        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task GetMenuItems_ReturnsAllItems()
    {
        var db = CreateDbContext();
        db.MenuItems.AddRange(
            new MenuItemEntity { Name = "Burger", Price = 5.99m, Category = "Burgers", IsAvailable = true },
            new MenuItemEntity { Name = "Fries", Price = 2.50m, Category = "Sides", IsAvailable = true }
        );
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var response = await service.GetMenuItems(new Empty(), new MockServerCallContext());

        Assert.Equal(2, response.Items.Count);
    }

    [Fact]
    public async Task GetMenuItem_ExistingItem_ReturnsItem()
    {
        var db = CreateDbContext();
        var entity = new MenuItemEntity
        {
            Name = "Cheeseburger",
            Price = 6.99m,
            Category = "Burgers",
            IsAvailable = true
        };
        db.MenuItems.Add(entity);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var item = await service.GetMenuItem(new BurgerIAM.Protos.Menu.GetMenuItemRequest
        {
            Id = entity.Id
        }, new MockServerCallContext());

        Assert.Equal("Cheeseburger", item.Name);
        Assert.Equal(6.99, item.Price);
    }

    [Fact]
    public async Task GetMenuItem_NonExistentItem_ThrowsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetMenuItem(new BurgerIAM.Protos.Menu.GetMenuItemRequest
            {
                Id = "nonexistent"
            }, new MockServerCallContext()));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateAvailability_ChangesStatus()
    {
        var db = CreateDbContext();
        var entity = new MenuItemEntity
        {
            Name = "Milkshake",
            Price = 3.50m,
            Category = "Drinks",
            IsAvailable = true
        };
        db.MenuItems.Add(entity);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var updated = await service.UpdateAvailability(new BurgerIAM.Protos.Menu.UpdateAvailabilityRequest
        {
            Id = entity.Id,
            IsAvailable = false
        }, new MockServerCallContext());

        Assert.False(updated.IsAvailable);

        var entityFromDb = await db.MenuItems.FindAsync(entity.Id);
        Assert.False(entityFromDb!.IsAvailable);
    }
}
