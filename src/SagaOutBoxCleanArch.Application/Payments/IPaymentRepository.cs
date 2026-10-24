using SagaOutBoxCleanArch.Domain.Payments;

namespace SagaOutBoxCleanArch.Application.Payments;

public interface IPaymentRepository
{
    Task<PaymentRecord?> GetBySagaIdAsync(Guid sagaId, CancellationToken cancellationToken);
    void Add(PaymentRecord payment);
}
