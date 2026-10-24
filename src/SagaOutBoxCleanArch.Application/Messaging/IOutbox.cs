namespace SagaOutBoxCleanArch.Application.Messaging;

/// <summary>
/// Stages messages for publication. Nothing is sent until the current unit of work is saved,
/// which commits the messages atomically with the business state change.
/// </summary>
public interface IOutbox
{
    void Enqueue(OutboxMessage message);
}

public static class OutboxExtensions
{
    public static void Enqueue<TPayload>(this IOutbox outbox, string type, TPayload payload, string destination)
    {
        outbox.Enqueue(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = type,
            Payload = MessageSerializer.Serialize(payload),
            Destination = destination,
            CreatedAt = DateTime.UtcNow
        });
    }
}
