using WasmFrontend.Models;
using WasmFrontend.Services;

namespace WasmFrontend.Tests;

public class CartServiceTests
{
    private static CartItem CreateTestItem(string id = "item1", string name = "Test Burger",
        double price = 5.99, int quantity = 1)
        => new(id, name, price, quantity);

    [Fact]
    public void AddItem_EmptyCart_AddsItem()
    {
        var cart = new CartService();
        cart.AddItem(CreateTestItem());

        Assert.Single(cart.Items);
        Assert.Equal(1, cart.TotalItems);
        Assert.Equal(5.99, cart.TotalAmount);
    }

    [Fact]
    public void AddItem_DuplicateItem_IncrementsQuantity()
    {
        var cart = new CartService();
        cart.AddItem(CreateTestItem(quantity: 1));
        cart.AddItem(CreateTestItem(quantity: 2));

        Assert.Single(cart.Items);
        Assert.Equal(3, cart.TotalItems);
    }

    [Fact]
    public void AddItem_MultipleItems_TracksCorrectly()
    {
        var cart = new CartService();
        cart.AddItem(CreateTestItem("item1", "Burger", 5.99));
        cart.AddItem(CreateTestItem("item2", "Fries", 2.99));
        cart.AddItem(CreateTestItem("item3", "Shake", 3.99));

        Assert.Equal(3, cart.Items.Count);
        Assert.Equal(3, cart.TotalItems);
        Assert.Equal(12.97, cart.TotalAmount, precision: 2);
    }

    [Fact]
    public void UpdateQuantity_ExistingItem_ChangesQuantity()
    {
        var cart = new CartService();
        cart.AddItem(CreateTestItem(quantity: 1));
        cart.UpdateQuantity("item1", 5);

        Assert.Equal(5, cart.TotalItems);
        Assert.Equal(29.95, cart.TotalAmount, precision: 2);
    }

    [Fact]
    public void UpdateQuantity_ZeroQuantity_RemovesItem()
    {
        var cart = new CartService();
        cart.AddItem(CreateTestItem());
        cart.UpdateQuantity("item1", 0);

        Assert.Empty(cart.Items);
        Assert.True(cart.IsEmpty);
    }

    [Fact]
    public void UpdateQuantity_NonexistentItem_DoesNothing()
    {
        var cart = new CartService();
        cart.AddItem(CreateTestItem());
        cart.UpdateQuantity("nonexistent", 5);

        Assert.Single(cart.Items);
    }

    [Fact]
    public void RemoveItem_ExistingItem_RemovesIt()
    {
        var cart = new CartService();
        cart.AddItem(CreateTestItem("item1"));
        cart.AddItem(CreateTestItem("item2"));
        cart.RemoveItem("item1");

        Assert.Single(cart.Items);
        Assert.Equal("item2", cart.Items[0].MenuItemId);
    }

    [Fact]
    public void Clear_NonEmptyCart_EmptiesCart()
    {
        var cart = new CartService();
        cart.AddItem(CreateTestItem());
        cart.AddItem(CreateTestItem("item2"));
        cart.Clear();

        Assert.Empty(cart.Items);
        Assert.True(cart.IsEmpty);
        Assert.Equal(0, cart.TotalItems);
        Assert.Equal(0, cart.TotalAmount);
    }

    [Fact]
    public void CartChanged_Event_FiresOnAdd()
    {
        var cart = new CartService();
        var fired = false;
        cart.CartChanged += () => fired = true;

        cart.AddItem(CreateTestItem());

        Assert.True(fired);
    }

    [Fact]
    public void CartChanged_Event_FiresOnRemove()
    {
        var cart = new CartService();
        cart.AddItem(CreateTestItem());

        var fired = false;
        cart.CartChanged += () => fired = true;
        cart.RemoveItem("item1");

        Assert.True(fired);
    }

    [Fact]
    public void CartChanged_Event_FiresOnClear()
    {
        var cart = new CartService();
        cart.AddItem(CreateTestItem());

        var fired = false;
        cart.CartChanged += () => fired = true;
        cart.Clear();

        Assert.True(fired);
    }

    [Fact]
    public void IsEmpty_NewCart_ReturnsTrue()
    {
        var cart = new CartService();
        Assert.True(cart.IsEmpty);
    }

    [Fact]
    public void TotalAmount_MultipleItems_CalculatesCorrectly()
    {
        var cart = new CartService();
        cart.AddItem(CreateTestItem("item1", "Burger", 5.99, 2));
        cart.AddItem(CreateTestItem("item2", "Fries", 2.99, 3));

        Assert.Equal(5, cart.TotalItems);
        Assert.Equal(20.95, cart.TotalAmount, precision: 2);
    }
}
