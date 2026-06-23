namespace BurgerIAM.Shared.Events;

public sealed record PaymentFailedEvent : IntegrationEvent
{
    public required string OrderId { get; init; }
    public required string PaymentId { get; init; }
    public decimal Amount { get; init; }
    public required string Reason { get; init; }
}
