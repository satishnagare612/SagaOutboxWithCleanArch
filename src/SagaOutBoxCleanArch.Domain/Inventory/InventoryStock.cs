namespace SagaOutBoxCleanArch.Domain.Inventory;

public sealed class InventoryStock
{
    public required string ProductId { get; set; }
    public int AvailableQuantity { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
