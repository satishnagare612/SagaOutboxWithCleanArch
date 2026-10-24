using Microsoft.EntityFrameworkCore;
using SagaOutBoxCleanArch.Application.Payments;
using SagaOutBoxCleanArch.Domain.Payments;

namespace SagaOutBoxCleanArch.Infrastructure.Persistence.Payments;

public sealed class PaymentRepository(PaymentDbContext db) : IPaymentRepository
{
    public Task<PaymentRecord?> GetBySagaIdAsync(Guid sagaId, CancellationToken cancellationToken) =>
        db.Payments.SingleOrDefaultAsync(payment => payment.SagaId == sagaId, cancellationToken);

    public void Add(PaymentRecord payment) => db.Payments.Add(payment);
}
