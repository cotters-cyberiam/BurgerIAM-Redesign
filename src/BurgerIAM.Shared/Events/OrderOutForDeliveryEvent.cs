namespace BurgerIAM.Shared.Events;

public sealed record OrderOutForDeliveryEvent : IntegrationEvent
{
    public required string OrderId { get; init; }
    public required string DriverId { get; init; }
    public DateTime? EstimatedDeliveryTime { get; init; }
}
