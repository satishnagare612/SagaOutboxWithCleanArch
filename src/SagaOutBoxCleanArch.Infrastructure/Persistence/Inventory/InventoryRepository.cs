using Microsoft.EntityFrameworkCore;
using SagaOutBoxCleanArch.Application.Inventory;
using SagaOutBoxCleanArch.Domain.Inventory;

namespace SagaOutBoxCleanArch.Infrastructure.Persistence.Inventory;

public sealed class InventoryRepository(InventoryDbContext db) : IInventoryRepository
{
    public Task<InventoryStock?> GetStockAsync(string productId, CancellationToken cancellationToken) =>
        db.Stocks.SingleOrDefaultAsync(stock => stock.ProductId == productId, cancellationToken);

    public void AddStock(InventoryStock stock) => db.Stocks.Add(stock);

    public Task<InventoryReservation?> GetReservationAsync(Guid sagaId, CancellationToken cancellationToken) =>
        db.Reservations.SingleOrDefaultAsync(reservation => reservation.SagaId == sagaId, cancellationToken);

    public void AddReservation(InventoryReservation reservation) => db.Reservations.Add(reservation);
}
