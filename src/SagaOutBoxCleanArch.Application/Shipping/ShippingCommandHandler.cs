using System.Text.Json;
using Microsoft.Extensions.Options;
using SagaOutBoxCleanArch.Application.Messaging;
using SagaOutBoxCleanArch.Contracts;
using SagaOutBoxCleanArch.Domain.Shipping;

namespace SagaOutBoxCleanArch.Application.Shipping;

public sealed class ShippingCommandHandler(
    IShipmentRepository shipments,
    IInbox inbox,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    IOptions<WorkflowOptions> workflow,
    IOptions<QueueNamesOptions> queues) : IdempotentMessageHandler(inbox, unitOfWork)
{
    protected override async Task ProcessAsync(string subject, string body, CancellationToken cancellationToken)
    {
        var command = MessageSerializer.Deserialize<WorkflowCommand>(body, "shipping command");
        var success = true;
        string? error = null;
        switch (subject)
        {
            case MessageTypes.ArrangeShipping:
            {
                var shipment = await shipments.GetBySagaIdAsync(command.SagaId, cancellationToken);
                if (shipment is not null)
                {
                    success = !shipment.Cancelled;
                    error = success ? null : "Shipment was already cancelled.";
                    break;
                }

                var failurePrefix = workflow.Value.ShippingFailureProductPrefix;
                if (failurePrefix.Length > 0 && command.ProductId.StartsWith(failurePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    success = false;
                    error = "Shipping could not be arranged for this product.";
                    break;
                }

                shipments.Add(new ShipmentRecord { SagaId = command.SagaId, OrderId = command.OrderId });
                break;
            }
            case MessageTypes.CancelShipping:
            {
                var shipment = await shipments.GetBySagaIdAsync(command.SagaId, cancellationToken);
                if (shipment is not null)
                {
                    shipment.Cancelled = true;
                }
                break;
            }
            default:
                throw new JsonException($"Unsupported shipping command '{subject}'.");
        }

        outbox.Enqueue(subject, new SagaReply(command.SagaId, command.OrderId, subject, success, error),
            queues.Value.SagaRepliesQueue);
    }
}
