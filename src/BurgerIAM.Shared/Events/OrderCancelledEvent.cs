namespace BurgerIAM.Shared.Events;

public sealed record OrderCancelledEvent : IntegrationEvent
{
    public required string OrderId { get; init; }
    public required string Reason { get; init; }
}
