namespace BurgerIAM.Shared.DTOs;

public sealed record OrderItemDto
{
    public required string MenuItemId { get; init; }
    public required string ItemName { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Subtotal => Quantity * UnitPrice;
}
