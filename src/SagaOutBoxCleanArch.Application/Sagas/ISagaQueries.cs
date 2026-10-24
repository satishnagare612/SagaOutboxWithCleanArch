using SagaOutBoxCleanArch.Contracts;

namespace SagaOutBoxCleanArch.Application.Sagas;

public interface ISagaQueries
{
    Task<SagaStatusResponse?> GetStatusAsync(Guid sagaId, CancellationToken cancellationToken);
}
