namespace SagaOutBoxCleanArch.Domain.Sagas;

public sealed class SagaInstance
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public required string ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public string Status { get; set; } = SagaStatuses.AwaitingOrder;
    public bool OrderCreated { get; set; }
    public bool InventoryReserved { get; set; }
    public bool PaymentProcessed { get; set; }
    public bool ShippingArranged { get; set; }
    public string CompensationPlan { get; set; } = "[]";
    public int CompensationIndex { get; set; }
    public int CompensationRetryCount { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class SagaStatuses
{
    public const string AwaitingOrder = "AwaitingOrder";
    public const string ReservingInventory = "ReservingInventory";
    public const string ProcessingPayment = "ProcessingPayment";
    public const string ArrangingShipping = "ArrangingShipping";
    public const string Compensating = "Compensating";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string ManualIntervention = "ManualIntervention";
}
