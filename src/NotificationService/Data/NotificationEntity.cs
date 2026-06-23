using System.ComponentModel.DataAnnotations;

namespace NotificationService.Data;

public sealed class NotificationEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(100)]
    public string CustomerId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Message { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
