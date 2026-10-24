using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SagaOutBoxCleanArch.Application.Inventory;
using SagaOutBoxCleanArch.Application.Messaging;
using SagaOutBoxCleanArch.Application.Payments;
using SagaOutBoxCleanArch.Application.Sagas;
using SagaOutBoxCleanArch.Application.Shipping;
using SagaOutBoxCleanArch.Contracts;
using SagaOutBoxCleanArch.Domain.Sagas;
using SagaOutBoxCleanArch.Infrastructure.Persistence.Inventory;
using SagaOutBoxCleanArch.Infrastructure.Persistence.Payments;
using SagaOutBoxCleanArch.Infrastructure.Persistence.Sagas;
using SagaOutBoxCleanArch.Infrastructure.Persistence.Shipping;

namespace SagaOutBoxCleanArch.Tests;

public sealed class ServiceBoundaryTests
{
    [Fact]
    public async Task InventoryReservationAndReleaseAreIdempotentAndWriteReplyToLocalOutbox()
    {
        var db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var handler = new InventoryCommandHandler(
            new InventoryRepository(db), db, db, db,
            Options.Create(new WorkflowOptions { DefaultAvailableQuantity = 3 }),
            Options.Create(new QueueNamesOptions()));
        var sagaId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var reserve = new WorkflowCommand(sagaId, orderId, "SKU-1", 2, 20m, MessageTypes.ReserveInventory);
        var body = MessageSerializer.Serialize(reserve);

        await handler.HandleAsync("inventory-reserve-1", MessageTypes.ReserveInventory, body, CancellationToken.None);
        await handler.HandleAsync("inventory-reserve-1", MessageTypes.ReserveInventory, body, CancellationToken.None);
        Assert.Equal(1, (await db.Stocks.SingleAsync()).AvailableQuantity);
        Assert.Equal(1, await db.Reservations.CountAsync());

        var release = reserve with { CommandType = MessageTypes.ReleaseInventory };
        var releaseBody = MessageSerializer.Serialize(release);
        await handler.HandleAsync("inventory-release-1", MessageTypes.ReleaseInventory, releaseBody, CancellationToken.None);
        await handler.HandleAsync("inventory-release-1", MessageTypes.ReleaseInventory, releaseBody, CancellationToken.None);

        Assert.Equal(3, (await db.Stocks.SingleAsync()).AvailableQuantity);
        Assert.True((await db.Reservations.SingleAsync()).Released);
        Assert.Equal(2, await db.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task PaymentDeclineDoesNotWritePaymentAndPersistsFailureReply()
    {
        var db = new PaymentDbContext(new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var handler = new PaymentCommandHandler(
            new PaymentRepository(db), db, db, db,
            Options.Create(new WorkflowOptions { DeclinedProductPrefix = "DECLINE-" }),
            Options.Create(new QueueNamesOptions()));
        var command = new WorkflowCommand(Guid.NewGuid(), Guid.NewGuid(), "DECLINE-1", 1, 9.99m, MessageTypes.ProcessPayment);
        var body = MessageSerializer.Serialize(command);

        await handler.HandleAsync("payment-message-1", command.CommandType, body, CancellationToken.None);
        await handler.HandleAsync("payment-message-1", command.CommandType, body, CancellationToken.None);

        Assert.Empty(await db.Payments.ToListAsync());
        var reply = MessageSerializer.Deserialize<SagaReply>((await db.OutboxMessages.SingleAsync()).Payload, "saga reply");
        Assert.False(reply.Success);
        Assert.Contains("declined", reply.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShippingFailureProducesReplyWithoutPersistingShipment()
    {
        var db = new ShippingDbContext(new DbContextOptionsBuilder<ShippingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var handler = new ShippingCommandHandler(
            new ShipmentRepository(db), db, db, db,
            Options.Create(new WorkflowOptions { ShippingFailureProductPrefix = "NO-SHIP-" }),
            Options.Create(new QueueNamesOptions()));
        var command = new WorkflowCommand(Guid.NewGuid(), Guid.NewGuid(), "NO-SHIP-1", 1, 9.99m, MessageTypes.ArrangeShipping);
        var body = MessageSerializer.Serialize(command);

        await handler.HandleAsync("shipping-message-1", command.CommandType, body, CancellationToken.None);
        await handler.HandleAsync("shipping-message-1", command.CommandType, body, CancellationToken.None);

        Assert.Empty(await db.Shipments.ToListAsync());
        var reply = MessageSerializer.Deserialize<SagaReply>((await db.OutboxMessages.SingleAsync()).Payload, "saga reply");
        Assert.False(reply.Success);
    }

    [Fact]
    public async Task OrchestratorCompensatesCompletedStepsInReverseOrder()
    {
        var db = new SagaDbContext(new DbContextOptionsBuilder<SagaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var saga = new SagaInstance
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            ProductId = "SKU-1",
            Quantity = 2,
            Amount = 20m,
            Status = SagaStatuses.ArrangingShipping,
            OrderCreated = true,
            InventoryReserved = true,
            PaymentProcessed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Sagas.Add(saga);
        await db.SaveChangesAsync();
        var handler = new SagaMessageHandler(
            new SagaRepository(db), db, db, db,
            Options.Create(new WorkflowOptions()),
            Options.Create(new QueueNamesOptions()),
            NullLogger<SagaMessageHandler>.Instance);
        var failure = new SagaReply(saga.Id, saga.OrderId, MessageTypes.ArrangeShipping, false, "No carrier.");

        var failureBody = MessageSerializer.Serialize(failure);
        await handler.HandleAsync("shipping-failure-1", MessageTypes.ArrangeShipping, failureBody, CancellationToken.None);
        await handler.HandleAsync("shipping-failure-1", MessageTypes.ArrangeShipping, failureBody, CancellationToken.None);

        Assert.Equal(SagaStatuses.Compensating, saga.Status);
        Assert.Equal(0, saga.CompensationRetryCount);
        Assert.Equal("[\"RefundPayment\",\"ReleaseInventory\",\"CancelOrder\"]", saga.CompensationPlan);
        var firstCommand = await db.OutboxMessages.SingleAsync();
        Assert.Equal(MessageTypes.RefundPayment, firstCommand.Type);
        Assert.Equal("payment-commands", firstCommand.Destination);

        var refundReply = new SagaReply(saga.Id, saga.OrderId, MessageTypes.RefundPayment, true);
        await handler.HandleAsync("refund-reply-1", MessageTypes.RefundPayment, MessageSerializer.Serialize(refundReply), CancellationToken.None);
        Assert.Equal(1, saga.CompensationIndex);
        Assert.Contains(await db.OutboxMessages.Select(message => message.Type).ToListAsync(), type => type == MessageTypes.ReleaseInventory);
    }
}
