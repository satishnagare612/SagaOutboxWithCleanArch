using SagaOutBoxCleanArch.Domain.Shipping;

namespace SagaOutBoxCleanArch.Application.Shipping;

public interface IShipmentRepository
{
    Task<ShipmentRecord?> GetBySagaIdAsync(Guid sagaId, CancellationToken cancellationToken);
    void Add(ShipmentRecord shipment);
}
