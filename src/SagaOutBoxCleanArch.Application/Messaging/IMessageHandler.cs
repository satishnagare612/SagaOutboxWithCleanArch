namespace SagaOutBoxCleanArch.Application.Messaging;

/// <summary>Handles one broker message. Implementations must be idempotent.</summary>
public interface IMessageHandler
{
    Task HandleAsync(string messageId, string subject, string body, CancellationToken cancellationToken);
}
