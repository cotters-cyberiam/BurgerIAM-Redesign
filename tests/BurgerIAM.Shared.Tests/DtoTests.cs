using BurgerIAM.Shared.DTOs;
using BurgerIAM.Shared.Enums;

namespace BurgerIAM.Shared.Tests;

public class DtoTests
{
    [Fact]
    public void MenuItemDto_CreatedCorrectly()
    {
        var dto = new MenuItemDto
        {
            Id = "item-1",
            Name = "Cheeseburger",
            Description = "A tasty burger",
            Price = 5.99m,
            Category = "Burgers",
            IsAvailable = true,
            ImageUrl = "/images/burger.jpg"
        };

        Assert.Equal("item-1", dto.Id);
        Assert.Equal("Cheeseburger", dto.Name);
        Assert.Equal("A tasty burger", dto.Description);
        Assert.Equal(5.99m, dto.Price);
        Assert.Equal("Burgers", dto.Category);
        Assert.True(dto.IsAvailable);
        Assert.Equal("/images/burger.jpg", dto.ImageUrl);
    }

    [Fact]
    public void OrderItemDto_Subtotal_CalculatedCorrectly()
    {
        var dto = new OrderItemDto
        {
            MenuItemId = "item-1",
            ItemName = "Fries",
            Quantity = 3,
            UnitPrice = 2.50m
        };

        Assert.Equal(7.50m, dto.Subtotal);
    }

    [Fact]
    public void OrderDto_CreatedCorrectly()
    {
        var now = DateTime.UtcNow;
        var items = new List<OrderItemDto>
        {
            new() { MenuItemId = "1", ItemName = "Burger", Quantity = 1, UnitPrice = 5.99m }
        };

        var dto = new OrderDto
        {
            Id = "order-1",
            CustomerId = "cust-1",
            CustomerEmail = "test@test.com",
            Items = items,
            TotalAmount = 5.99m,
            Status = OrderStatus.Pending,
            DeliveryAddress = "123 Main St",
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal("order-1", dto.Id);
        Assert.Equal(OrderStatus.Pending, dto.Status);
        Assert.Single(dto.Items);
    }

    [Fact]
    public void PaymentDto_CreatedCorrectly()
    {
        var dto = new PaymentDto
        {
            Id = "pay-1",
            OrderId = "order-1",
            Amount = 15.00m,
            Status = PaymentStatus.Confirmed,
            Method = "CreditCard",
            CreatedAt = DateTime.UtcNow
        };

        Assert.Equal("pay-1", dto.Id);
        Assert.Equal(PaymentStatus.Confirmed, dto.Status);
        Assert.Equal("CreditCard", dto.Method);
    }

    [Fact]
    public void UserDto_CreatedCorrectly()
    {
        var dto = new UserDto
        {
            Id = "user-1",
            Email = "test@test.com",
            Name = "Test User",
            Role = "Customer"
        };

        Assert.Equal("user-1", dto.Id);
        Assert.Equal("test@test.com", dto.Email);
        Assert.Equal("Test User", dto.Name);
        Assert.Equal("Customer", dto.Role);
    }
}
