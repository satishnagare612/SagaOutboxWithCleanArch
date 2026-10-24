using SagaOutBoxCleanArch.Domain.Inventory;

namespace SagaOutBoxCleanArch.Application.Inventory;

public interface IInventoryRepository
{
    Task<InventoryStock?> GetStockAsync(string productId, CancellationToken cancellationToken);
    void AddStock(InventoryStock stock);
    Task<InventoryReservation?> GetReservationAsync(Guid sagaId, CancellationToken cancellationToken);
    void AddReservation(InventoryReservation reservation);
}
