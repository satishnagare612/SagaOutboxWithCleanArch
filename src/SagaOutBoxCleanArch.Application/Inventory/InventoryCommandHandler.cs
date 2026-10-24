using System.Text.Json;
using Microsoft.Extensions.Options;
using SagaOutBoxCleanArch.Application.Messaging;
using SagaOutBoxCleanArch.Contracts;
using SagaOutBoxCleanArch.Domain.Inventory;

namespace SagaOutBoxCleanArch.Application.Inventory;

public sealed class InventoryCommandHandler(
    IInventoryRepository inventory,
    IInbox inbox,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    IOptions<WorkflowOptions> workflow,
    IOptions<QueueNamesOptions> queues) : IdempotentMessageHandler(inbox, unitOfWork)
{
    protected override async Task ProcessAsync(string subject, string body, CancellationToken cancellationToken)
    {
        var command = MessageSerializer.Deserialize<WorkflowCommand>(body, "inventory command");
        var success = true;
        string? error = null;
        switch (subject)
        {
            case MessageTypes.ReserveInventory:
            {
                var reservation = await inventory.GetReservationAsync(command.SagaId, cancellationToken);
                if (reservation is not null)
                {
                    success = !reservation.Released;
                    error = success ? null : "Reservation was already released.";
                    break;
                }

                var stock = await inventory.GetStockAsync(command.ProductId, cancellationToken);
                if (stock is null)
                {
                    stock = new InventoryStock
                    {
                        ProductId = command.ProductId,
                        AvailableQuantity = workflow.Value.DefaultAvailableQuantity
                    };
                    inventory.AddStock(stock);
                }

                if (stock.AvailableQuantity < command.Quantity)
                {
                    success = false;
                    error = "Insufficient stock.";
                    break;
                }

                stock.AvailableQuantity -= command.Quantity;
                inventory.AddReservation(new InventoryReservation
                {
                    SagaId = command.SagaId,
                    OrderId = command.OrderId,
                    ProductId = command.ProductId,
                    Quantity = command.Quantity
                });
                break;
            }
            case MessageTypes.ReleaseInventory:
            {
                var reservation = await inventory.GetReservationAsync(command.SagaId, cancellationToken);
                if (reservation is not null && !reservation.Released)
                {
                    var stock = await inventory.GetStockAsync(reservation.ProductId, cancellationToken)
                        ?? throw new InvalidOperationException($"Stock for product '{reservation.ProductId}' was not found.");
                    stock.AvailableQuantity = checked(stock.AvailableQuantity + reservation.Quantity);
                    reservation.Released = true;
                }
                break;
            }
            default:
                throw new JsonException($"Unsupported inventory command '{subject}'.");
        }

        outbox.Enqueue(subject, new SagaReply(command.SagaId, command.OrderId, subject, success, error),
            queues.Value.SagaRepliesQueue);
    }
}
