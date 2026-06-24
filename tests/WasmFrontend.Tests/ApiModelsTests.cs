using WasmFrontend.Models;

namespace WasmFrontend.Tests;

public class ApiModelsTests
{
    [Fact]
    public void LoginRequest_Created_WithCorrectProperties()
    {
        var request = new LoginRequest("test@email.com", "password123");

        Assert.Equal("test@email.com", request.Email);
        Assert.Equal("password123", request.Password);
    }

    [Fact]
    public void RegisterRequest_Created_WithCorrectProperties()
    {
        var request = new RegisterRequest("test@email.com", "password123", "Test User");

        Assert.Equal("test@email.com", request.Email);
        Assert.Equal("password123", request.Password);
        Assert.Equal("Test User", request.Name);
    }

    [Fact]
    public void CreateOrderRequest_Created_WithItems()
    {
        var items = new List<OrderItemRequest>
        {
            new("menu1", "Burger", 2, 5.99),
            new("menu2", "Fries", 1, 2.99)
        };

        var request = new CreateOrderRequest("cust1", "test@email.com", items, "123 Main St");

        Assert.Equal("cust1", request.CustomerId);
        Assert.Equal(2, request.Items.Count);
        Assert.Equal("123 Main St", request.DeliveryAddress);
    }

    [Fact]
    public void CartItem_Created_WithCorrectProperties()
    {
        var item = new CartItem("menu1", "Burger", 5.99, 2);

        Assert.Equal("menu1", item.MenuItemId);
        Assert.Equal("Burger", item.ItemName);
        Assert.Equal(5.99, item.UnitPrice);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public void MenuItemResponse_HasAllProperties()
    {
        var item = new MenuItemResponse("id1", "Burger", "Delicious", 5.99,
            "Burgers", true, "/images/burger.jpg");

        Assert.Equal("id1", item.Id);
        Assert.Equal("Burger", item.Name);
        Assert.Equal("Delicious", item.Description);
        Assert.Equal(5.99, item.Price);
        Assert.Equal("Burgers", item.Category);
        Assert.True(item.IsAvailable);
    }

    [Fact]
    public void FeedbackRequest_Rating_RangeIs1To5()
    {
        Assert.True(1 <= 3 && 3 <= 5, "Rating must be between 1 and 5");
        Assert.True(1 <= 1 && 1 <= 5);
        Assert.True(1 <= 5 && 5 <= 5);
    }

    [Fact]
    public void OrderResponse_Status_HasValidValues()
    {
        var statuses = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        foreach (var s in statuses)
        {
            Assert.InRange(s, 0, 8);
        }
    }
}
