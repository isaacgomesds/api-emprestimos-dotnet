using ApiEmprestimos.Domain.Entities;

namespace ApiEmprestimos.Domain.Repositories;

public interface IBookRepository
{
    Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Book?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Book>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<BookReservationResult> TryReserveCopyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> TryUpdateStockAsync(Guid id, int totalCopies, CancellationToken cancellationToken = default);
    Task<(int TotalCopies, int AvailableCopies)?> GetStockAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task<bool> TryIncrementAvailableCopiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Book book, CancellationToken cancellationToken = default);
    Task UpdateAsync(Book book, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
