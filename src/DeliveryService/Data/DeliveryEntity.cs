using System.ComponentModel.DataAnnotations;

namespace DeliveryService.Data;

public sealed class DeliveryEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(100)]
    public string OrderId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? DriverId { get; set; }

    [MaxLength(200)]
    public string? DriverName { get; set; }

    public int Status { get; set; }

    [Required, MaxLength(500)]
    public string DeliveryAddress { get; set; } = string.Empty;

    public DateTime? EstimatedDeliveryTime { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class DriverEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public bool IsAvailable { get; set; } = true;
}
