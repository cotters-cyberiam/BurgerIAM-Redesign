namespace BurgerIAM.EventBus;

internal sealed record EventSubscription
{
    public required Type EventType { get; init; }
    public required Delegate Handler { get; init; }
}
