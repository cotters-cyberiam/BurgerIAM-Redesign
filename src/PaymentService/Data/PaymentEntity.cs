using System.ComponentModel.DataAnnotations;

namespace PaymentService.Data;

public sealed class PaymentEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(100)]
    public string OrderId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public int Status { get; set; }

    [Required, MaxLength(50)]
    public string Method { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
