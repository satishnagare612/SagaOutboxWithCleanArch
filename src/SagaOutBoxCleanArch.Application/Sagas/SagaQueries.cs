using SagaOutBoxCleanArch.Contracts;

namespace SagaOutBoxCleanArch.Application.Sagas;

public sealed class SagaQueries(ISagaRepository sagas) : ISagaQueries
{
    public async Task<SagaStatusResponse?> GetStatusAsync(Guid sagaId, CancellationToken cancellationToken)
    {
        var saga = await sagas.GetByIdReadOnlyAsync(sagaId, cancellationToken);
        return saga is null
            ? null
            : new SagaStatusResponse(saga.Id, saga.OrderId, saga.Status, saga.OrderCreated,
                saga.InventoryReserved, saga.PaymentProcessed, saga.ShippingArranged, saga.FailureReason,
                saga.CreatedAt, saga.UpdatedAt);
    }
}
