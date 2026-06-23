using BurgerIAM.Shared.Enums;

namespace BurgerIAM.Shared.DTOs;

public sealed record OrderDto
{
    public required string Id { get; init; }
    public required string CustomerId { get; init; }
    public required string CustomerEmail { get; init; }
    public required List<OrderItemDto> Items { get; init; }
    public decimal TotalAmount { get; init; }
    public OrderStatus Status { get; init; }
    public required string DeliveryAddress { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
