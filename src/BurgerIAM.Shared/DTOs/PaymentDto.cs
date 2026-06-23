using BurgerIAM.Shared.Enums;

namespace BurgerIAM.Shared.DTOs;

public sealed record PaymentDto
{
    public required string Id { get; init; }
    public required string OrderId { get; init; }
    public decimal Amount { get; init; }
    public PaymentStatus Status { get; init; }
    public required string Method { get; init; }
    public DateTime CreatedAt { get; init; }
}
