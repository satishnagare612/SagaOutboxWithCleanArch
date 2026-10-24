namespace SagaOutBoxCleanArch.Infrastructure.Messaging;

public sealed class OutboxOptions
{
    public int PollIntervalSeconds { get; set; } = 2;
    public int BatchSize { get; set; } = 50;
    public int MaxRetries { get; set; } = 10;
    public int LeaseDurationSeconds { get; set; } = 120;
}
