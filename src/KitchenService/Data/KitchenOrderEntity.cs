using System.ComponentModel.DataAnnotations;

namespace KitchenService.Data;

public sealed class KitchenOrderEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(100)]
    public string OrderId { get; set; } = string.Empty;

    public int Status { get; set; }

    [MaxLength(100)]
    public string? AssignedStation { get; set; }

    public DateTime? EstimatedReadyTime { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
