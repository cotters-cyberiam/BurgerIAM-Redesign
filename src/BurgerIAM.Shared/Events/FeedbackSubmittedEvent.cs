namespace BurgerIAM.Shared.Events;

public sealed record FeedbackSubmittedEvent : IntegrationEvent
{
    public required string OrderId { get; init; }
    public required string CustomerId { get; init; }
    public int Rating { get; init; }
    public string? Comment { get; init; }
}
