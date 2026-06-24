using WasmFrontend.Models;

namespace WasmFrontend.Services;

public sealed class CartService
{
    private readonly List<CartItem> _items = [];

    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();
    public int TotalItems => _items.Sum(i => i.Quantity);
    public double TotalAmount => _items.Sum(i => i.UnitPrice * i.Quantity);
    public bool IsEmpty => _items.Count == 0;

    public event Action? CartChanged;

    public void AddItem(CartItem item)
    {
        var existing = _items.FirstOrDefault(i => i.MenuItemId == item.MenuItemId);
        if (existing is not null)
        {
            _items.Remove(existing);
            _items.Add(existing with { Quantity = existing.Quantity + item.Quantity });
        }
        else
        {
            _items.Add(item);
        }
        CartChanged?.Invoke();
    }

    public void UpdateQuantity(string menuItemId, int quantity)
    {
        var item = _items.FirstOrDefault(i => i.MenuItemId == menuItemId);
        if (item is null) return;

        if (quantity <= 0)
        {
            _items.Remove(item);
        }
        else
        {
            _items.Remove(item);
            _items.Add(item with { Quantity = quantity });
        }
        CartChanged?.Invoke();
    }

    public void RemoveItem(string menuItemId)
    {
        _items.RemoveAll(i => i.MenuItemId == menuItemId);
        CartChanged?.Invoke();
    }

    public void Clear()
    {
        _items.Clear();
        CartChanged?.Invoke();
    }
}
