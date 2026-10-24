namespace SagaOutBoxCleanArch.Domain.Shipping;

public sealed class ShipmentRecord
{
    public Guid SagaId { get; set; }
    public Guid OrderId { get; set; }
    public bool Cancelled { get; set; }
}
