namespace SagaOutBoxCleanArch.Application.Messaging;

/// <summary>
/// Template for inbox-deduplicated handling: skip already-processed messages, run the handler,
/// then record the message and commit state, inbox entry and outbox messages in one transaction.
/// </summary>
public abstract class IdempotentMessageHandler(IInbox inbox, IUnitOfWork unitOfWork) : IMessageHandler
{
    public async Task HandleAsync(string messageId, string subject, string body, CancellationToken cancellationToken)
    {
        if (await inbox.ExistsAsync(messageId, cancellationToken))
        {
            return;
        }

        await ProcessAsync(subject, body, cancellationToken);

        inbox.MarkProcessed(messageId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Applies the message to local state and stages any outgoing messages. Must not save.</summary>
    protected abstract Task ProcessAsync(string subject, string body, CancellationToken cancellationToken);
}
