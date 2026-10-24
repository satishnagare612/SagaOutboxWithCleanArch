using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SagaOutBoxCleanArch.Application.Inventory;
using SagaOutBoxCleanArch.Application.Messaging;
using SagaOutBoxCleanArch.Application.Orders;
using SagaOutBoxCleanArch.Application.Payments;
using SagaOutBoxCleanArch.Application.Sagas;
using SagaOutBoxCleanArch.Application.Shipping;
using SagaOutBoxCleanArch.Infrastructure.Messaging;
using SagaOutBoxCleanArch.Infrastructure.Persistence;
using SagaOutBoxCleanArch.Infrastructure.Persistence.Inventory;
using SagaOutBoxCleanArch.Infrastructure.Persistence.Orders;
using SagaOutBoxCleanArch.Infrastructure.Persistence.Payments;
using SagaOutBoxCleanArch.Infrastructure.Persistence.Sagas;
using SagaOutBoxCleanArch.Infrastructure.Persistence.Shipping;

namespace SagaOutBoxCleanArch.Infrastructure;

/// <summary>
/// Composition helpers. Each host calls exactly one <c>Add*Infrastructure</c> method, which wires that
/// service's database, repositories, outbox dispatcher and queue consumer(s).
/// </summary>
public static class DependencyInjection
{
    /// <summary>The order API keeps working without Service Bus; outbox rows simply accumulate until it is configured.</summary>
    public static IServiceCollection AddOrderInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDatabase<OrderDbContext>(configuration);
        services.AddScoped<IOrderRepository, OrderRepository>();
        if (ServiceBusRegistration.IsConfigured(configuration))
        {
            services.AddServiceBusMessaging<OrderDbContext>(configuration,
                QueueNamesOptions.FromConfiguration(configuration).OrderCommandsQueue);
        }
        return services;
    }

    public static IServiceCollection AddSagaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDatabase<SagaDbContext>(configuration);
        services.AddScoped<ISagaRepository, SagaRepository>();
        var queues = QueueNamesOptions.FromConfiguration(configuration);
        services.AddServiceBusMessaging<SagaDbContext>(configuration, queues.SagaRepliesQueue, queues.SagaStartQueue);
        return services;
    }

    public static IServiceCollection AddInventoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDatabase<InventoryDbContext>(configuration);
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddServiceBusMessaging<InventoryDbContext>(configuration,
            QueueNamesOptions.FromConfiguration(configuration).InventoryCommandsQueue);
        return services;
    }

    public static IServiceCollection AddPaymentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDatabase<PaymentDbContext>(configuration);
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddServiceBusMessaging<PaymentDbContext>(configuration,
            QueueNamesOptions.FromConfiguration(configuration).PaymentCommandsQueue);
        return services;
    }

    public static IServiceCollection AddShippingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDatabase<ShippingDbContext>(configuration);
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddServiceBusMessaging<ShippingDbContext>(configuration,
            QueueNamesOptions.FromConfiguration(configuration).ShippingCommandsQueue);
        return services;
    }

    private static IServiceCollection AddServiceDatabase<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : ServiceDbContext
    {
        var connectionString = configuration.GetConnectionString("ServiceDatabase")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:ServiceDatabase for this service.");
        services.AddDbContext<TContext>(options => options.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            sql.MigrationsAssembly(typeof(TContext).Assembly.FullName);
        }));

        // The service's context is the single unit of work shared by its repositories, inbox and outbox.
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<TContext>());
        services.AddScoped<IOutbox>(provider => provider.GetRequiredService<TContext>());
        services.AddScoped<IInbox>(provider => provider.GetRequiredService<TContext>());
        return services;
    }

    private static IServiceCollection AddServiceBusMessaging<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        params string[] consumedQueues)
        where TContext : DbContext
    {
        services.AddServiceBusClient(configuration);

        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection("Outbox"))
            .Validate(options => options.PollIntervalSeconds > 0
                && options.BatchSize > 0
                && options.MaxRetries > 0
                && options.LeaseDurationSeconds >= 30,
                "Outbox polling and lease settings must be positive and leases must be at least 30 seconds.")
            .ValidateOnStart();
        services.AddHostedService<OutboxDispatcher<TContext>>();

        // One worker consumes every registered queue.
        foreach (var queue in consumedQueues)
        {
            services.AddSingleton(new QueueWorkerOptions(queue));
        }
        services.AddHostedService<QueueMessageWorker>();
        return services;
    }
}
