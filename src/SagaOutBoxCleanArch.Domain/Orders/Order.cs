namespace SagaOutBoxCleanArch.Domain.Orders;

public sealed class Order
{
    public Guid Id { get; set; }
    public Guid SagaId { get; set; }
    public required string ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Status { get; set; } = OrderStatuses.Pending;
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public static class OrderStatuses
{
    public const string Pending = "Pending";
    public const string Fulfilled = "Fulfilled";
    public const string Cancelled = "Cancelled";
}
