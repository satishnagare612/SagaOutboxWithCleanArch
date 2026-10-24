namespace SagaOutBoxCleanArch.Application.Messaging;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
