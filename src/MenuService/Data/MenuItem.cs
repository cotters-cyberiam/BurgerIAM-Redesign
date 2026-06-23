using System.ComponentModel.DataAnnotations;

namespace MenuService.Data;

public class MenuItemEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public decimal Price { get; set; }

    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    public bool IsAvailable { get; set; } = true;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
