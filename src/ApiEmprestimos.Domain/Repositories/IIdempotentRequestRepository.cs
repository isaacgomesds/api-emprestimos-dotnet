using ApiEmprestimos.Domain.Entities;

namespace ApiEmprestimos.Domain.Repositories;

public interface IIdempotentRequestRepository
{
    Task<bool> TryCreateAsync(IdempotentRequest request, CancellationToken cancellationToken = default);
    Task<IdempotentRequest?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
