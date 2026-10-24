namespace SagaOutBoxCleanArch.Application.Messaging;

public interface IInbox
{
    Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken);

    /// <summary>Records the message as handled; persisted with the next unit-of-work save.</summary>
    void MarkProcessed(string messageId);
}
