using Microsoft.EntityFrameworkCore;
using SagaOutBoxCleanArch.Domain.Inventory;

namespace SagaOutBoxCleanArch.Infrastructure.Persistence.Inventory;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : ServiceDbContext(options)
{
    public DbSet<InventoryStock> Stocks => Set<InventoryStock>();
    public DbSet<InventoryReservation> Reservations => Set<InventoryReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryStock>(entity =>
        {
            entity.ToTable("InventoryStocks", "dbo");
            entity.HasKey(stock => stock.ProductId);
            entity.Property(stock => stock.ProductId).HasMaxLength(100);
            entity.Property(stock => stock.RowVersion).IsRowVersion();
        });
        modelBuilder.Entity<InventoryReservation>(entity =>
        {
            entity.ToTable("InventoryReservations", "dbo");
            entity.HasKey(reservation => reservation.SagaId);
            entity.Property(reservation => reservation.ProductId).HasMaxLength(100).IsRequired();
        });
        base.OnModelCreating(modelBuilder);
    }
}
