namespace SagaOutBoxCleanArch.Application.Messaging;

/// <summary>A message persisted in the same transaction as the state change that produced it.</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public required string Type { get; set; }
    public required string Payload { get; set; }
    public required string Destination { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? Error { get; set; }
    public bool IsDeadLetter { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
}

/// <summary>Records a handled broker message ID so redelivered messages are ignored.</summary>
public sealed class InboxMessage
{
    public required string Id { get; set; }
    public DateTime ReceivedAt { get; set; }
}
