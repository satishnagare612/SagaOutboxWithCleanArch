using SagaOutBoxCleanArch.Domain.Sagas;

namespace SagaOutBoxCleanArch.Application.Sagas;

public interface ISagaRepository
{
    void Add(SagaInstance saga);

    /// <summary>Loads a tracked saga so it can be modified.</summary>
    Task<SagaInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads an untracked saga for read-only use.</summary>
    Task<SagaInstance?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken);
}
