using System.Data;
using System.Data.Common;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SagaOutBoxCleanArch.Infrastructure.Messaging;

public sealed class OutboxDispatcher<TContext>(
    IServiceScopeFactory scopeFactory,
    ServiceBusClient serviceBusClient,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher<TContext>> logger) : BackgroundService where TContext : DbContext
{
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly Dictionary<string, ServiceBusSender> _senders = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var claimed = await ClaimBatchAsync(settings, stoppingToken);
                foreach (var message in claimed)
                {
                    await DispatchAsync(message, settings, stoppingToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox dispatcher loop failed; retrying after {PollIntervalSeconds} seconds.", settings.PollIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
            }
        }
    }

    private async Task<IReadOnlyList<ClaimedMessage>> ClaimBatchAsync(OutboxOptions settings, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                ;WITH candidates AS
                (
                    SELECT TOP (@BatchSize) *
                    FROM dbo.OutboxMessages WITH (UPDLOCK, READPAST, ROWLOCK)
                    WHERE ProcessedAt IS NULL
                      AND IsDeadLetter = 0
                      AND RetryCount < @MaxRetries
                      AND (NextAttemptAt IS NULL OR NextAttemptAt <= SYSUTCDATETIME())
                      AND (LeaseExpiresAt IS NULL OR LeaseExpiresAt <= SYSUTCDATETIME())
                    ORDER BY CreatedAt
                )
                UPDATE candidates
                SET LeaseId = @LeaseId,
                    LeaseExpiresAt = @LeaseExpiresAt
                OUTPUT INSERTED.Id, INSERTED.Type, INSERTED.Payload, INSERTED.Destination;
                """;
            AddParameter(command, "@BatchSize", settings.BatchSize);
            AddParameter(command, "@MaxRetries", settings.MaxRetries);
            AddParameter(command, "@LeaseId", _instanceId);
            AddParameter(command, "@LeaseExpiresAt", DateTime.UtcNow.AddSeconds(settings.LeaseDurationSeconds));

            var messages = new List<ClaimedMessage>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    messages.Add(new ClaimedMessage(
                        reader.GetGuid(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3)));
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return messages;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task DispatchAsync(ClaimedMessage message, OutboxOptions settings, CancellationToken cancellationToken)
    {
        try
        {
            if (!_senders.TryGetValue(message.Destination, out var sender))
            {
                sender = serviceBusClient.CreateSender(message.Destination);
                _senders.Add(message.Destination, sender);
            }

            await sender.SendMessageAsync(new ServiceBusMessage(message.Payload)
            {
                MessageId = message.Id.ToString("D"),
                Subject = message.Type,
                ContentType = "application/json"
            }, cancellationToken);
            await UpdateMessageAsync(message.Id, true, null, settings, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Failed to publish outbox message {OutboxMessageId} to {Destination}.", message.Id, message.Destination);
            var error = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message;
            await UpdateMessageAsync(message.Id, false, error, settings, cancellationToken);
        }
    }

    private async Task UpdateMessageAsync(Guid messageId, bool sent, string? error, OutboxOptions settings, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sent
                ? "UPDATE dbo.OutboxMessages SET ProcessedAt = SYSUTCDATETIME(), Error = NULL, NextAttemptAt = NULL, LeaseId = NULL, LeaseExpiresAt = NULL WHERE Id = @Id AND LeaseId = @LeaseId AND ProcessedAt IS NULL;"
                : "UPDATE dbo.OutboxMessages SET RetryCount = RetryCount + 1, Error = @Error, IsDeadLetter = CASE WHEN RetryCount + 1 >= @MaxRetries THEN 1 ELSE 0 END, NextAttemptAt = CASE WHEN RetryCount + 1 >= @MaxRetries THEN NULL ELSE DATEADD(second, CASE WHEN POWER(CAST(2 AS float), RetryCount) * 5 > 300 THEN 300 ELSE CAST(POWER(CAST(2 AS float), RetryCount) * 5 AS int) END, SYSUTCDATETIME()) END, LeaseId = NULL, LeaseExpiresAt = NULL WHERE Id = @Id AND LeaseId = @LeaseId AND ProcessedAt IS NULL;";
            AddParameter(command, "@Id", messageId);
            AddParameter(command, "@LeaseId", _instanceId);
            if (!sent)
            {
                AddParameter(command, "@Error", error!);
                AddParameter(command, "@MaxRetries", settings.MaxRetries);
            }

            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected != 1)
            {
                logger.LogWarning("Outbox message {OutboxMessageId} no longer belongs to this worker lease.", messageId);
            }
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync();
        }
    }

    private sealed record ClaimedMessage(Guid Id, string Type, string Payload, string Destination);
}
