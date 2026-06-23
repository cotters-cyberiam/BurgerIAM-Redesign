using System.ComponentModel.DataAnnotations;

namespace FeedbackService.Data;

public sealed class FeedbackEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(100)]
    public string OrderId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string CustomerId { get; set; } = string.Empty;

    public int Rating { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
