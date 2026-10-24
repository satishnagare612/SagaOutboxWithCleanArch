using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SagaOutBoxCleanArch.Application.Inventory;
using SagaOutBoxCleanArch.Application.Messaging;
using SagaOutBoxCleanArch.Application.Orders;
using SagaOutBoxCleanArch.Application.Payments;
using SagaOutBoxCleanArch.Application.Sagas;
using SagaOutBoxCleanArch.Application.Shipping;

namespace SagaOutBoxCleanArch.Application;

public static class DependencyInjection
{
    /// <summary>Binds <see cref="WorkflowOptions"/> and <see cref="QueueNamesOptions"/>. Every host needs this.</summary>
    public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WorkflowOptions>().Bind(configuration.GetSection(WorkflowOptions.SectionName));
        services.AddOptions<QueueNamesOptions>().Bind(configuration.GetSection(QueueNamesOptions.SectionName));
        return services;
    }

    public static IServiceCollection AddOrderApplication(this IServiceCollection services)
    {
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IMessageHandler, OrderCommandHandler>();
        return services;
    }

    public static IServiceCollection AddSagaApplication(this IServiceCollection services)
    {
        services.AddScoped<ISagaQueries, SagaQueries>();
        services.AddScoped<IMessageHandler, SagaMessageHandler>();
        return services;
    }

    public static IServiceCollection AddInventoryApplication(this IServiceCollection services)
    {
        services.AddScoped<IMessageHandler, InventoryCommandHandler>();
        return services;
    }

    public static IServiceCollection AddPaymentApplication(this IServiceCollection services)
    {
        services.AddScoped<IMessageHandler, PaymentCommandHandler>();
        return services;
    }

    public static IServiceCollection AddShippingApplication(this IServiceCollection services)
    {
        services.AddScoped<IMessageHandler, ShippingCommandHandler>();
        return services;
    }
}
