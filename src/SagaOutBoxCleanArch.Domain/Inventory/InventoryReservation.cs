namespace SagaOutBoxCleanArch.Domain.Inventory;

public sealed class InventoryReservation
{
    public Guid SagaId { get; set; }
    public Guid OrderId { get; set; }
    public required string ProductId { get; set; }
    public int Quantity { get; set; }
    public bool Released { get; set; }
}
