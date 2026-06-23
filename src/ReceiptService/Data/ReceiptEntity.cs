using System.ComponentModel.DataAnnotations;

namespace ReceiptService.Data;

public sealed class ReceiptEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(100)]
    public string OrderId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string CustomerId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? CustomerEmail { get; set; }

    public double TotalAmount { get; set; }

    [MaxLength(500)]
    public string? ItemsJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
