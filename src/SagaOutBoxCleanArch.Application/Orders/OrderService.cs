using Microsoft.Extensions.Options;
using SagaOutBoxCleanArch.Application.Messaging;
using SagaOutBoxCleanArch.Contracts;
using SagaOutBoxCleanArch.Domain.Orders;

namespace SagaOutBoxCleanArch.Application.Orders;

public sealed class OrderService(
    IOrderRepository orders,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    IOptions<QueueNamesOptions> queues) : IOrderService
{
    private const int RecentOrderCount = 100;

    public async Task<OrderDto> CreateAsync(CreateOrderInput input, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            SagaId = Guid.NewGuid(),
            ProductId = input.ProductId.Trim(),
            Quantity = input.Quantity,
            UnitPrice = input.UnitPrice,
            CreatedAt = now
        };
        var command = new CreateOrderCommand(order.Id, order.SagaId, order.ProductId,
            order.Quantity, order.UnitPrice, now);

        orders.Add(order);
        outbox.Enqueue(MessageTypes.CreateOrderCommand, command, queues.Value.OrderCommandsQueue);
        outbox.Enqueue(MessageTypes.StartSaga, command, queues.Value.SagaStartQueue);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return OrderDto.From(order);
    }

    public async Task<OrderDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdReadOnlyAsync(id, cancellationToken);
        return order is null ? null : OrderDto.From(order);
    }

    public async Task<IReadOnlyList<OrderDto>> GetRecentAsync(CancellationToken cancellationToken)
    {
        var recent = await orders.GetRecentAsync(RecentOrderCount, cancellationToken);
        return recent.Select(OrderDto.From).ToList();
    }

    public async Task<OrderDto?> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(id, cancellationToken);
        if (order is null)
        {
            return null;
        }

        if (order.Status == OrderStatuses.Cancelled)
        {
            return OrderDto.From(order);
        }

        order.Status = OrderStatuses.Cancelled;
        var command = new WorkflowCommand(order.SagaId, order.Id, order.ProductId,
            order.Quantity, order.UnitPrice * order.Quantity, MessageTypes.CancelOrder);
        outbox.Enqueue(MessageTypes.CancelOrder, command, queues.Value.OrderCommandsQueue);
        outbox.Enqueue(MessageTypes.CancelSaga, command, queues.Value.SagaRepliesQueue);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return OrderDto.From(order);
    }
}
