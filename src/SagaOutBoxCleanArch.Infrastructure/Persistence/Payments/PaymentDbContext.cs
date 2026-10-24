using Microsoft.EntityFrameworkCore;
using SagaOutBoxCleanArch.Domain.Payments;

namespace SagaOutBoxCleanArch.Infrastructure.Persistence.Payments;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : ServiceDbContext(options)
{
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.ToTable("Payments", "dbo");
            entity.HasKey(payment => payment.SagaId);
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
        });
        base.OnModelCreating(modelBuilder);
    }
}
