namespace SagaOutBoxCleanArch.Application.Messaging;

/// <summary>Workflow tuning and simulation switches, bound from the <c>Workflow</c> configuration section.</summary>
public sealed class WorkflowOptions
{
    public const string SectionName = "Workflow";

    /// <summary>Stock created the first time a product is requested.</summary>
    public int DefaultAvailableQuantity { get; set; } = 1000;

    /// <summary>Product IDs starting with this prefix simulate a declined payment.</summary>
    public string DeclinedProductPrefix { get; set; } = "DECLINE-";

    /// <summary>Product IDs starting with this prefix simulate a shipping failure. Empty disables the simulation.</summary>
    public string ShippingFailureProductPrefix { get; set; } = string.Empty;

    public int MaxCompensationRetries { get; set; } = 5;
}
