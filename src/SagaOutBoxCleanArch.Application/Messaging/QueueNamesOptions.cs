using Microsoft.Extensions.Configuration;

namespace SagaOutBoxCleanArch.Application.Messaging;

/// <summary>Queue names, bound from the <c>ServiceBus</c> configuration section.</summary>
public sealed class QueueNamesOptions
{
    public const string SectionName = "ServiceBus";

    public string OrderCommandsQueue { get; set; } = "order-commands";
    public string InventoryCommandsQueue { get; set; } = "inventory-commands";
    public string PaymentCommandsQueue { get; set; } = "payment-commands";
    public string ShippingCommandsQueue { get; set; } = "shipping-commands";
    public string SagaRepliesQueue { get; set; } = "saga-replies";
    public string SagaStartQueue { get; set; } = "saga-start";

    public static QueueNamesOptions FromConfiguration(IConfiguration configuration) =>
        configuration.GetSection(SectionName).Get<QueueNamesOptions>() ?? new QueueNamesOptions();
}
