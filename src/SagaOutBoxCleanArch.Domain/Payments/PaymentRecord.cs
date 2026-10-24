namespace SagaOutBoxCleanArch.Domain.Payments;

public sealed class PaymentRecord
{
    public Guid SagaId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public bool Refunded { get; set; }
}
