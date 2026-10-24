namespace SagaOutBoxCleanArch.Contracts;

public sealed record CreateOrderCommand(
    Guid OrderId,
    Guid SagaId,
    string ProductId,
    int Quantity,
    decimal UnitPrice,
    DateTime CreatedAt);

public sealed record WorkflowCommand(
    Guid SagaId,
    Guid OrderId,
    string ProductId,
    int Quantity,
    decimal Amount,
    string CommandType);

public sealed record SagaReply(
    Guid SagaId,
    Guid OrderId,
    string CommandType,
    bool Success,
    string? Error = null);
