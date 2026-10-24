using Microsoft.EntityFrameworkCore;
using SagaOutBoxCleanArch.Domain.Sagas;

namespace SagaOutBoxCleanArch.Infrastructure.Persistence.Sagas;

public sealed class SagaDbContext(DbContextOptions<SagaDbContext> options) : ServiceDbContext(options)
{
    public DbSet<SagaInstance> Sagas => Set<SagaInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SagaInstance>(entity =>
        {
            entity.ToTable("Sagas", "dbo");
            entity.HasKey(saga => saga.Id);
            entity.HasIndex(saga => saga.OrderId).IsUnique();
            entity.Property(saga => saga.Status).HasMaxLength(32).IsRequired();
            entity.Property(saga => saga.CompensationPlan).HasMaxLength(500).IsRequired();
            entity.Property(saga => saga.FailureReason).HasMaxLength(2000);
            entity.Property(saga => saga.ProductId).HasMaxLength(100).IsRequired();
            entity.Property(saga => saga.Amount).HasPrecision(18, 2);
            entity.Property(saga => saga.RowVersion).IsRowVersion();
        });
        base.OnModelCreating(modelBuilder);
    }
}
