namespace BurgerIAM.Shared.Events;

public sealed record OrderInProgressEvent : IntegrationEvent
{
    public required string OrderId { get; init; }
    public DateTime? EstimatedReadyTime { get; init; }
}
