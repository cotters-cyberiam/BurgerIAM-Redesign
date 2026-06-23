using System.ComponentModel.DataAnnotations;

namespace OrderService.Data;

public sealed class OrderEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(100)]
    public string CustomerId { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string CustomerEmail { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public int Status { get; set; }

    [Required, MaxLength(500)]
    public string DeliveryAddress { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<OrderItemEntity> Items { get; set; } = [];
}

public sealed class OrderItemEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string OrderId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string MenuItemId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}
