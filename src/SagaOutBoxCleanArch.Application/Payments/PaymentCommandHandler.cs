using System.Text.Json;
using Microsoft.Extensions.Options;
using SagaOutBoxCleanArch.Application.Messaging;
using SagaOutBoxCleanArch.Contracts;
using SagaOutBoxCleanArch.Domain.Payments;

namespace SagaOutBoxCleanArch.Application.Payments;

public sealed class PaymentCommandHandler(
    IPaymentRepository payments,
    IInbox inbox,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    IOptions<WorkflowOptions> workflow,
    IOptions<QueueNamesOptions> queues) : IdempotentMessageHandler(inbox, unitOfWork)
{
    protected override async Task ProcessAsync(string subject, string body, CancellationToken cancellationToken)
    {
        var command = MessageSerializer.Deserialize<WorkflowCommand>(body, "payment command");
        var success = true;
        string? error = null;
        switch (subject)
        {
            case MessageTypes.ProcessPayment:
            {
                var payment = await payments.GetBySagaIdAsync(command.SagaId, cancellationToken);
                if (payment is not null)
                {
                    success = !payment.Refunded;
                    error = success ? null : "Payment was already refunded.";
                    break;
                }

                var declinePrefix = workflow.Value.DeclinedProductPrefix;
                if (declinePrefix.Length > 0 && command.ProductId.StartsWith(declinePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    success = false;
                    error = "Payment was declined for this product.";
                    break;
                }

                payments.Add(new PaymentRecord
                {
                    SagaId = command.SagaId,
                    OrderId = command.OrderId,
                    Amount = command.Amount
                });
                break;
            }
            case MessageTypes.RefundPayment:
            {
                var payment = await payments.GetBySagaIdAsync(command.SagaId, cancellationToken);
                if (payment is not null)
                {
                    payment.Refunded = true;
                }
                break;
            }
            default:
                throw new JsonException($"Unsupported payment command '{subject}'.");
        }

        outbox.Enqueue(subject, new SagaReply(command.SagaId, command.OrderId, subject, success, error),
            queues.Value.SagaRepliesQueue);
    }
}
