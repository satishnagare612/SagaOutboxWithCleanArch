using Microsoft.EntityFrameworkCore;
using SagaOutBoxCleanArch.Application.Shipping;
using SagaOutBoxCleanArch.Domain.Shipping;

namespace SagaOutBoxCleanArch.Infrastructure.Persistence.Shipping;

public sealed class ShipmentRepository(ShippingDbContext db) : IShipmentRepository
{
    public Task<ShipmentRecord?> GetBySagaIdAsync(Guid sagaId, CancellationToken cancellationToken) =>
        db.Shipments.SingleOrDefaultAsync(shipment => shipment.SagaId == sagaId, cancellationToken);

    public void Add(ShipmentRecord shipment) => db.Shipments.Add(shipment);
}
