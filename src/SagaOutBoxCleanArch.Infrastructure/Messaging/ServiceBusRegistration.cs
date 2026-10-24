using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SagaOutBoxCleanArch.Infrastructure.Messaging;

public static class ServiceBusRegistration
{
    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["ServiceBus:ConnectionString"])
        || !string.IsNullOrWhiteSpace(configuration["ServiceBus:Namespace"]);

    public static IServiceCollection AddServiceBusClient(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["ServiceBus:ConnectionString"];
        var fullyQualifiedNamespace = configuration["ServiceBus:Namespace"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton(new ServiceBusClient(connectionString));
        }
        else if (!string.IsNullOrWhiteSpace(fullyQualifiedNamespace))
        {
            services.AddSingleton(new ServiceBusClient(fullyQualifiedNamespace, new DefaultAzureCredential()));
        }
        else
        {
            throw new InvalidOperationException("Configure ServiceBus:Namespace or ServiceBus:ConnectionString.");
        }

        return services;
    }
}
