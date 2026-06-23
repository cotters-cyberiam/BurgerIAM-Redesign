using BurgerIAM.Shared.DTOs;

namespace BurgerIAM.Shared.Events;

public sealed record OrderPlacedEvent : IntegrationEvent
{
    public required string OrderId { get; init; }
    public required string CustomerId { get; init; }
    public required string CustomerEmail { get; init; }
    public required List<OrderItemDto> Items { get; init; }
    public decimal TotalAmount { get; init; }
    public required string DeliveryAddress { get; init; }
}
