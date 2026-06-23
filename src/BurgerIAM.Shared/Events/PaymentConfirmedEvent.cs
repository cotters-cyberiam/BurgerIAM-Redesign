namespace BurgerIAM.Shared.Events;

public sealed record PaymentConfirmedEvent : IntegrationEvent
{
    public required string OrderId { get; init; }
    public required string PaymentId { get; init; }
    public decimal Amount { get; init; }
}
