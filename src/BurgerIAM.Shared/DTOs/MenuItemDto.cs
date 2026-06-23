namespace BurgerIAM.Shared.DTOs;

public sealed record MenuItemDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public required string Category { get; init; }
    public bool IsAvailable { get; init; }
    public string? ImageUrl { get; init; }
}
