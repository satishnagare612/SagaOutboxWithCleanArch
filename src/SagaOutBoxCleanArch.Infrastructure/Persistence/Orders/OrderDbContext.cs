using Microsoft.EntityFrameworkCore;
using SagaOutBoxCleanArch.Domain.Orders;

namespace SagaOutBoxCleanArch.Infrastructure.Persistence.Orders;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : ServiceDbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders", "dbo");
            entity.HasKey(order => order.Id);
            entity.HasIndex(order => order.SagaId).IsUnique();
            entity.Property(order => order.ProductId).HasMaxLength(100).IsRequired();
            entity.Property(order => order.Status).HasMaxLength(32).IsRequired();
            entity.Property(order => order.UnitPrice).HasPrecision(18, 2);
            entity.Property(order => order.RowVersion).IsRowVersion();
        });
        base.OnModelCreating(modelBuilder);
    }
}
