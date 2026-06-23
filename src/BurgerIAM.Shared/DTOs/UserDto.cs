namespace BurgerIAM.Shared.DTOs;

public sealed record UserDto
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
}
