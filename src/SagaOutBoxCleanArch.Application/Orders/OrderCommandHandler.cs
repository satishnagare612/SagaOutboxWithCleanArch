using System.Text.Json;
using Microsoft.Extensions.Options;
using SagaOutBoxCleanArch.Application.Messaging;
using SagaOutBoxCleanArch.Contracts;
using SagaOutBoxCleanArch.Domain.Orders;

namespace SagaOutBoxCleanArch.Application.Orders;

/// <summary>Handles commands sent by the orchestrator to the order service.</summary>
public sealed class OrderCommandHandler(
    IOrderRepository orders,
    IInbox inbox,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    IOptions<QueueNamesOptions> queues) : IdempotentMessageHandler(inbox, unitOfWork)
{
    protected override async Task ProcessAsync(string subject, string body, CancellationToken cancellationToken)
    {
        if (subject == MessageTypes.CreateOrderCommand)
        {
            var create = MessageSerializer.Deserialize<CreateOrderCommand>(body, "create order message");
            var order = await orders.GetByIdAsync(create.OrderId, cancellationToken);
            if (order is null || order.SagaId != create.SagaId)
            {
                throw new JsonException("Create order message does not match an order in this service database.");
            }

            Reply(new SagaReply(create.SagaId, create.OrderId, MessageTypes.OrderCreated, true));
        }
        else if (subject == MessageTypes.CancelOrder)
        {
            var command = MessageSerializer.Deserialize<WorkflowCommand>(body, "cancel order message");
            var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
            if (order is not null && order.SagaId != command.SagaId)
            {
                throw new JsonException("Cancel order message does not match the order saga.");
            }

            if (order is not null)
            {
                order.Status = OrderStatuses.Cancelled;
            }

            Reply(new SagaReply(command.SagaId, command.OrderId, MessageTypes.CancelOrder, true));
        }
        else
        {
            throw new JsonException($"Unsupported order command '{subject}'.");
        }
    }

    private void Reply(SagaReply reply) =>
        outbox.Enqueue(reply.CommandType, reply, queues.Value.SagaRepliesQueue);
}
