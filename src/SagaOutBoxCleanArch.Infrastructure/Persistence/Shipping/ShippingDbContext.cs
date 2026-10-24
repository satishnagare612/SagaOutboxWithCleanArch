using Microsoft.EntityFrameworkCore;
using SagaOutBoxCleanArch.Domain.Shipping;

namespace SagaOutBoxCleanArch.Infrastructure.Persistence.Shipping;

public sealed class ShippingDbContext(DbContextOptions<ShippingDbContext> options) : ServiceDbContext(options)
{
    public DbSet<ShipmentRecord> Shipments => Set<ShipmentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShipmentRecord>(entity =>
        {
            entity.ToTable("Shipments", "dbo");
            entity.HasKey(shipment => shipment.SagaId);
        });
        base.OnModelCreating(modelBuilder);
    }
}
