using Microsoft.EntityFrameworkCore;
using SagaOutBoxCleanArch.Application.Messaging;

namespace SagaOutBoxCleanArch.Infrastructure.Persistence;

/// <summary>
/// Base context for every service database. It owns the outbox and inbox tables and implements the
/// application's messaging and unit-of-work abstractions, so each service commits state, inbox entry and
/// outgoing messages in a single transaction.
/// </summary>
public abstract class ServiceDbContext(DbContextOptions options) : DbContext(options), IOutbox, IInbox, IUnitOfWork
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    void IOutbox.Enqueue(OutboxMessage message) => OutboxMessages.Add(message);

    Task<bool> IInbox.ExistsAsync(string messageId, CancellationToken cancellationToken) =>
        InboxMessages.AnyAsync(message => message.Id == messageId, cancellationToken);

    void IInbox.MarkProcessed(string messageId) =>
        InboxMessages.Add(new InboxMessage { Id = messageId, ReceivedAt = DateTime.UtcNow });

    Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessages", "dbo");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Id).HasMaxLength(128);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages", "dbo");
            entity.HasKey(message => message.Id);
            entity.HasIndex(message => new { message.ProcessedAt, message.IsDeadLetter, message.NextAttemptAt, message.LeaseExpiresAt, message.CreatedAt });
            entity.Property(message => message.Type).HasMaxLength(200).IsRequired();
            entity.Property(message => message.Destination).HasMaxLength(260).IsRequired();
            entity.Property(message => message.Payload).IsRequired();
            entity.Property(message => message.Error).HasMaxLength(2000);
        });

        base.OnModelCreating(modelBuilder);
    }
}
