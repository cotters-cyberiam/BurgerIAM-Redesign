namespace BurgerIAM.Shared.Events;

public sealed record OrderDeliveredEvent : IntegrationEvent
{
    public required string OrderId { get; init; }
}
