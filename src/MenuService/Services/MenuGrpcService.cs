using Grpc.Core;
using MenuService.Data;
using Microsoft.EntityFrameworkCore;
using ProtoCommon = BurgerIAM.Protos.Common;
using ProtoMenu = BurgerIAM.Protos.Menu;
using MenuItemProto = BurgerIAM.Protos.Menu.MenuItem;

namespace MenuService.Services;

public sealed class MenuGrpcService : ProtoMenu.MenuService.MenuServiceBase
{
    private readonly AppDbContext _db;

    public MenuGrpcService(AppDbContext db)
    {
        _db = db;
    }

    public override async Task<ProtoMenu.GetMenuItemsResponse> GetMenuItems(ProtoCommon.Empty request, ServerCallContext context)
    {
        var items = await _db.MenuItems
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Name)
            .ToListAsync(context.CancellationToken);

        var response = new ProtoMenu.GetMenuItemsResponse();
        response.Items.AddRange(items.Select(MapToProto));

        return response;
    }

    public override async Task<MenuItemProto> GetMenuItem(ProtoMenu.GetMenuItemRequest request, ServerCallContext context)
    {
        var item = await _db.MenuItems.FindAsync([request.Id], cancellationToken: context.CancellationToken);

        if (item is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"MenuItem {request.Id} not found"));
        }

        return MapToProto(item);
    }

    public override async Task<MenuItemProto> UpdateAvailability(ProtoMenu.UpdateAvailabilityRequest request, ServerCallContext context)
    {
        var item = await _db.MenuItems.FindAsync([request.Id], cancellationToken: context.CancellationToken);

        if (item is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"MenuItem {request.Id} not found"));
        }

        item.IsAvailable = request.IsAvailable;
        await _db.SaveChangesAsync(context.CancellationToken);

        return MapToProto(item);
    }

    private static MenuItemProto MapToProto(MenuItemEntity item)
    {
        return new MenuItemProto
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description ?? string.Empty,
            Price = (double)item.Price,
            Category = item.Category,
            IsAvailable = item.IsAvailable,
            ImageUrl = item.ImageUrl ?? string.Empty
        };
    }
}
