namespace SagaOutBoxCleanArch.Application.Orders;

public interface IOrderService
{
    /// <summary>Creates the order and stages the <c>CreateOrderCommand</c> and <c>StartSaga</c> outbox messages in one transaction.</summary>
    Task<OrderDto> CreateAsync(CreateOrderInput input, CancellationToken cancellationToken);

    Task<OrderDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderDto>> GetRecentAsync(CancellationToken cancellationToken);

    /// <summary>Cancels the order and asks the orchestrator to compensate. Returns <c>null</c> when the order does not exist.</summary>
    Task<OrderDto?> CancelAsync(Guid id, CancellationToken cancellationToken);
}
