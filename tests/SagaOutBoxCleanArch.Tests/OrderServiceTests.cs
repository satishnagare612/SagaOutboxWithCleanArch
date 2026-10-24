using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SagaOutBoxCleanArch.Application.Messaging;
using SagaOutBoxCleanArch.Application.Orders;
using SagaOutBoxCleanArch.Contracts;
using SagaOutBoxCleanArch.Domain.Orders;
using SagaOutBoxCleanArch.Infrastructure.Persistence.Orders;

namespace SagaOutBoxCleanArch.Tests;

public sealed class OrderServiceTests
{
    private static (OrderService Service, OrderDbContext Db) CreateService()
    {
        var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var service = new OrderService(new OrderRepository(db), db, db, Options.Create(new QueueNamesOptions()));
        return (service, db);
    }

    [Fact]
    public async Task CreateStoresOrderAndStagesCreateAndStartSagaMessages()
    {
        var (service, db) = CreateService();

        var created = await service.CreateAsync(new CreateOrderInput("  SKU-1 ", 2, 10m), CancellationToken.None);

        Assert.Equal("SKU-1", created.ProductId);
        Assert.Equal(OrderStatuses.Pending, created.Status);
        Assert.Equal(1, await db.Orders.CountAsync());
        var outbox = await db.OutboxMessages.ToListAsync();
        Assert.Contains(outbox, m => m.Type == MessageTypes.CreateOrderCommand && m.Destination == "order-commands");
        Assert.Contains(outbox, m => m.Type == MessageTypes.StartSaga && m.Destination == "saga-start");
    }

    [Fact]
    public async Task CancelIsIdempotentAndReturnsNullForUnknownOrder()
    {
        var (service, db) = CreateService();
        var created = await service.CreateAsync(new CreateOrderInput("SKU-1", 1, 5m), CancellationToken.None);

        var cancelled = await service.CancelAsync(created.Id, CancellationToken.None);
        await service.CancelAsync(created.Id, CancellationToken.None);

        Assert.Equal(OrderStatuses.Cancelled, cancelled!.Status);
        // 2 messages from create + 2 from the first cancel only.
        Assert.Equal(4, await db.OutboxMessages.CountAsync());
        Assert.Null(await service.CancelAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
