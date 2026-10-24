using System.ComponentModel.DataAnnotations;

namespace SagaOutBoxCleanArch.Api.Requests;

public sealed class CreateOrderRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string ProductId { get; init; }

    [Range(1, 10000)]
    public int Quantity { get; init; }

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal UnitPrice { get; init; }
}
