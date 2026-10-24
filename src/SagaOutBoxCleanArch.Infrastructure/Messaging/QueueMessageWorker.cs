using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SagaOutBoxCleanArch.Application.Messaging;

namespace SagaOutBoxCleanArch.Infrastructure.Messaging;

public sealed class QueueMessageWorker(
    ServiceBusClient client,
    IServiceScopeFactory scopeFactory,
    IEnumerable<QueueWorkerOptions> queueOptions,
    ILogger<QueueMessageWorker> logger) : IHostedService, IAsyncDisposable
{
    private readonly List<ServiceBusProcessor> _processors = [];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var options in queueOptions)
        {
            if (string.IsNullOrWhiteSpace(options.QueueName))
            {
                throw new InvalidOperationException("The Service Bus queue name must be configured.");
            }

            var processor = client.CreateProcessor(options.QueueName, new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = options.MaxConcurrentCalls,
                PrefetchCount = options.PrefetchCount
            });
            processor.ProcessMessageAsync += ProcessMessageAsync;
            processor.ProcessErrorAsync += ProcessErrorAsync;
            _processors.Add(processor);
            await processor.StartProcessingAsync(cancellationToken);
            logger.LogInformation("Started processing queue {QueueName}.", options.QueueName);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var processor in _processors)
        {
            await processor.StopProcessingAsync(cancellationToken);
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
        try
        {
            await handler.HandleAsync(args.Message.MessageId, args.Message.Subject, args.Message.Body.ToString(), args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogError(exception, "Dead-lettering invalid message {MessageId} from {QueueName}.", args.Message.MessageId, args.EntityPath);
            await args.DeadLetterMessageAsync(args.Message, "InvalidMessage", exception.Message, args.CancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !args.CancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Processing failed for message {MessageId} from {QueueName}; abandoning for retry.",
                args.Message.MessageId, args.EntityPath);
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus error for {EntityPath} in {Namespace}.", args.EntityPath, args.FullyQualifiedNamespace);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var processor in _processors)
        {
            await processor.DisposeAsync();
        }
        _processors.Clear();
    }
}

public sealed record QueueWorkerOptions(string QueueName, int MaxConcurrentCalls = 1, int PrefetchCount = 0);
