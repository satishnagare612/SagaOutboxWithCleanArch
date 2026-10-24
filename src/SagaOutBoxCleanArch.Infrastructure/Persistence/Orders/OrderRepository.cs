using Microsoft.EntityFrameworkCore;
using SagaOutBoxCleanArch.Application.Orders;
using SagaOutBoxCleanArch.Domain.Orders;

namespace SagaOutBoxCleanArch.Infrastructure.Persistence.Orders;

public sealed class OrderRepository(OrderDbContext db) : IOrderRepository
{
    public void Add(Order order) => db.Orders.Add(order);

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.SingleOrDefaultAsync(order => order.Id == id, cancellationToken);

    public Task<Order?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking().SingleOrDefaultAsync(order => order.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Order>> GetRecentAsync(int count, CancellationToken cancellationToken) =>
        await db.Orders.AsNoTracking()
            .OrderByDescending(order => order.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
}
