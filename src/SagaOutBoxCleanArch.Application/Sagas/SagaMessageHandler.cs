using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SagaOutBoxCleanArch.Application.Messaging;
using SagaOutBoxCleanArch.Contracts;
using SagaOutBoxCleanArch.Domain.Sagas;

namespace SagaOutBoxCleanArch.Application.Sagas;

/// <summary>
/// The saga orchestrator: starts sagas, advances them as replies arrive, and drives reverse-order compensation.
/// </summary>
public sealed class SagaMessageHandler(
    ISagaRepository sagas,
    IInbox inbox,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    IOptions<WorkflowOptions> workflow,
    IOptions<QueueNamesOptions> queues,
    ILogger<SagaMessageHandler> logger) : IdempotentMessageHandler(inbox, unitOfWork)
{
    protected override async Task ProcessAsync(string subject, string body, CancellationToken cancellationToken)
    {
        if (subject == MessageTypes.StartSaga)
        {
            await StartSagaAsync(body, cancellationToken);
        }
        else if (subject == MessageTypes.CancelSaga)
        {
            await CancelSagaAsync(body, cancellationToken);
        }
        else
        {
            await HandleReplyAsync(subject, body, cancellationToken);
        }
    }

    private async Task HandleReplyAsync(string subject, string body, CancellationToken cancellationToken)
    {
        if (subject is not (MessageTypes.OrderCreated or MessageTypes.ReserveInventory or MessageTypes.ProcessPayment
            or MessageTypes.ArrangeShipping or MessageTypes.ReleaseInventory or MessageTypes.RefundPayment
            or MessageTypes.CancelShipping or MessageTypes.CancelOrder))
        {
            throw new JsonException($"Unsupported saga message '{subject}'.");
        }

        var reply = MessageSerializer.Deserialize<SagaReply>(body, "saga reply");
        var saga = await sagas.GetByIdAsync(reply.SagaId, cancellationToken)
            ?? throw new InvalidOperationException($"Saga '{reply.SagaId}' has not been started yet.");
        if (saga.OrderId != reply.OrderId)
        {
            throw new JsonException("The reply order does not match the saga.");
        }

        if (IsTerminal(saga.Status))
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (saga.Status == SagaStatuses.Compensating)
        {
            HandleCompensationReply(saga, reply, now);
        }
        else
        {
            HandleForwardReply(saga, reply, now);
        }
    }

    private async Task CancelSagaAsync(string body, CancellationToken cancellationToken)
    {
        var command = MessageSerializer.Deserialize<WorkflowCommand>(body, "cancel saga message");
        var saga = await sagas.GetByIdAsync(command.SagaId, cancellationToken)
            ?? throw new InvalidOperationException($"Saga '{command.SagaId}' has not been started yet.");
        if (saga.OrderId != command.OrderId)
        {
            throw new JsonException("The cancel request order does not match its saga.");
        }
        if (IsTerminal(saga.Status))
        {
            return;
        }

        saga.FailureReason = "Cancelled by client request.";
        BeginCompensation(saga, DateTime.UtcNow);
    }

    private async Task StartSagaAsync(string body, CancellationToken cancellationToken)
    {
        var start = MessageSerializer.Deserialize<CreateOrderCommand>(body, "start saga message");
        var now = DateTime.UtcNow;
        var existing = await sagas.GetByIdAsync(start.SagaId, cancellationToken);
        if (existing is not null)
        {
            if (existing.OrderId != start.OrderId)
            {
                throw new JsonException("Saga ID is already associated with another order.");
            }
            return;
        }

        sagas.Add(new SagaInstance
        {
            Id = start.SagaId,
            OrderId = start.OrderId,
            ProductId = start.ProductId,
            Quantity = start.Quantity,
            Amount = start.UnitPrice * start.Quantity,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private void HandleForwardReply(SagaInstance saga, SagaReply reply, DateTime now)
    {
        if (!IsExpectedForwardReply(saga.Status, reply.CommandType))
        {
            logger.LogInformation("Ignoring stale reply {ReplyType} for saga {SagaId} in state {SagaStatus}.", reply.CommandType, saga.Id, saga.Status);
            return;
        }

        saga.UpdatedAt = now;
        if (!reply.Success)
        {
            saga.FailureReason = reply.Error ?? $"Step '{reply.CommandType}' failed.";
            BeginCompensation(saga, now);
            return;
        }

        switch (reply.CommandType)
        {
            case MessageTypes.OrderCreated:
                saga.OrderCreated = true;
                saga.Status = SagaStatuses.ReservingInventory;
                QueueCommand(saga, MessageTypes.ReserveInventory);
                break;
            case MessageTypes.ReserveInventory:
                saga.InventoryReserved = true;
                saga.Status = SagaStatuses.ProcessingPayment;
                QueueCommand(saga, MessageTypes.ProcessPayment);
                break;
            case MessageTypes.ProcessPayment:
                saga.PaymentProcessed = true;
                saga.Status = SagaStatuses.ArrangingShipping;
                QueueCommand(saga, MessageTypes.ArrangeShipping);
                break;
            case MessageTypes.ArrangeShipping:
                saga.ShippingArranged = true;
                saga.Status = SagaStatuses.Completed;
                break;
        }
    }

    private void HandleCompensationReply(SagaInstance saga, SagaReply reply, DateTime now)
    {
        var plan = MessageSerializer.Deserialize<List<string>>(saga.CompensationPlan, "compensation plan");
        if (saga.CompensationIndex >= plan.Count || plan[saga.CompensationIndex] != reply.CommandType)
        {
            logger.LogInformation("Ignoring stale compensation reply {ReplyType} for saga {SagaId}.", reply.CommandType, saga.Id);
            return;
        }

        saga.UpdatedAt = now;
        if (!reply.Success)
        {
            saga.CompensationRetryCount++;
            var maxRetries = workflow.Value.MaxCompensationRetries;
            if (saga.CompensationRetryCount > maxRetries)
            {
                saga.Status = SagaStatuses.ManualIntervention;
                saga.FailureReason = $"Compensation '{reply.CommandType}' failed after {maxRetries} retries: {reply.Error}";
                return;
            }

            QueueCommand(saga, reply.CommandType);
            return;
        }

        saga.CompensationIndex++;
        saga.CompensationRetryCount = 0;
        if (saga.CompensationIndex == plan.Count)
        {
            saga.Status = SagaStatuses.Failed;
            return;
        }

        QueueCommand(saga, plan[saga.CompensationIndex]);
    }

    private void BeginCompensation(SagaInstance saga, DateTime now)
    {
        var plan = new List<string>();
        if (saga.ShippingArranged) plan.Add(MessageTypes.CancelShipping);
        if (saga.PaymentProcessed) plan.Add(MessageTypes.RefundPayment);
        if (saga.InventoryReserved) plan.Add(MessageTypes.ReleaseInventory);
        if (saga.OrderCreated) plan.Add(MessageTypes.CancelOrder);

        saga.CompensationPlan = MessageSerializer.Serialize(plan);
        saga.CompensationIndex = 0;
        saga.CompensationRetryCount = 0;
        saga.UpdatedAt = now;
        if (plan.Count == 0)
        {
            saga.Status = SagaStatuses.Failed;
            return;
        }

        saga.Status = SagaStatuses.Compensating;
        QueueCommand(saga, plan[0]);
    }

    private void QueueCommand(SagaInstance saga, string commandType)
    {
        var command = new WorkflowCommand(saga.Id, saga.OrderId, saga.ProductId, saga.Quantity, saga.Amount, commandType);
        outbox.Enqueue(commandType, command, ResolveQueue(commandType));
    }

    private string ResolveQueue(string commandType) => commandType switch
    {
        MessageTypes.ReserveInventory or MessageTypes.ReleaseInventory => queues.Value.InventoryCommandsQueue,
        MessageTypes.ProcessPayment or MessageTypes.RefundPayment => queues.Value.PaymentCommandsQueue,
        MessageTypes.ArrangeShipping or MessageTypes.CancelShipping => queues.Value.ShippingCommandsQueue,
        _ => queues.Value.OrderCommandsQueue
    };

    private static bool IsExpectedForwardReply(string status, string commandType) => (status, commandType) switch
    {
        (SagaStatuses.AwaitingOrder, MessageTypes.OrderCreated) => true,
        (SagaStatuses.ReservingInventory, MessageTypes.ReserveInventory) => true,
        (SagaStatuses.ProcessingPayment, MessageTypes.ProcessPayment) => true,
        (SagaStatuses.ArrangingShipping, MessageTypes.ArrangeShipping) => true,
        _ => false
    };

    private static bool IsTerminal(string status) =>
        status is SagaStatuses.Completed or SagaStatuses.Failed or SagaStatuses.ManualIntervention;
}
