namespace BurgerIAM.Shared.Events;

public sealed record MenuItemUpdatedEvent : IntegrationEvent
{
    public required string ItemId { get; init; }
    public required string Name { get; init; }
    public decimal NewPrice { get; init; }
    public bool IsAvailable { get; init; }
}
