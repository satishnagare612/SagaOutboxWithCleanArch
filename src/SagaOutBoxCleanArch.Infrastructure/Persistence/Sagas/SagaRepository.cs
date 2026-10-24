using Microsoft.EntityFrameworkCore;
using SagaOutBoxCleanArch.Application.Sagas;
using SagaOutBoxCleanArch.Domain.Sagas;

namespace SagaOutBoxCleanArch.Infrastructure.Persistence.Sagas;

public sealed class SagaRepository(SagaDbContext db) : ISagaRepository
{
    public void Add(SagaInstance saga) => db.Sagas.Add(saga);

    public Task<SagaInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Sagas.SingleOrDefaultAsync(saga => saga.Id == id, cancellationToken);

    public Task<SagaInstance?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        db.Sagas.AsNoTracking().SingleOrDefaultAsync(saga => saga.Id == id, cancellationToken);
}
