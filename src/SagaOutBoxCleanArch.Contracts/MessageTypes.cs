namespace SagaOutBoxCleanArch.Contracts;

/// <summary>
/// Message subjects shared by the orchestrator and the participating services.
/// These values travel on the wire (Service Bus <c>Subject</c>), so they must not change casually.
/// </summary>
public static class MessageTypes
{
    // Order service
    public const string CreateOrderCommand = "CreateOrderCommand";
    public const string CancelOrder = "CancelOrder";
    public const string OrderCreated = "OrderCreated";

    // Saga lifecycle (consumed by the orchestrator)
    public const string StartSaga = "StartSaga";
    public const string CancelSaga = "CancelSaga";

    // Inventory service
    public const string ReserveInventory = "ReserveInventory";
    public const string ReleaseInventory = "ReleaseInventory";

    // Payment service
    public const string ProcessPayment = "ProcessPayment";
    public const string RefundPayment = "RefundPayment";

    // Shipping service
    public const string ArrangeShipping = "ArrangeShipping";
    public const string CancelShipping = "CancelShipping";
}
