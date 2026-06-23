namespace BurgerIAM.Shared.Events;

public sealed record OrderReadyEvent : IntegrationEvent
{
    public required string OrderId { get; init; }
}
