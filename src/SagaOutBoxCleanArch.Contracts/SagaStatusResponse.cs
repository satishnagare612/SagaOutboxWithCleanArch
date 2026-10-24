namespace SagaOutBoxCleanArch.Contracts;

public sealed record SagaStatusResponse(
    Guid SagaId,
    Guid OrderId,
    string Status,
    bool OrderCreated,
    bool InventoryReserved,
    bool PaymentProcessed,
    bool ShippingArranged,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);
