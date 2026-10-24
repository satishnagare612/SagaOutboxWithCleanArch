using SagaOutBoxCleanArch.Domain.Orders;

namespace SagaOutBoxCleanArch.Application.Orders;

public interface IOrderRepository
{
    void Add(Order order);

    /// <summary>Loads a tracked order so it can be modified.</summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads an untracked order for read-only use.</summary>
    Task<Order?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Order>> GetRecentAsync(int count, CancellationToken cancellationToken);
}
